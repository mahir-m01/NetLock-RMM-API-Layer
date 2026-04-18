using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ControlIT.Api.Application;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IJwtService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private const string RefreshCookieName = "refresh_token";

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordResetTokenRepository resetTokens,
        IJwtService jwt,
        IPasswordHasher hasher,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _resetTokens = resetTokens;
        _jwt = jwt;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext ctx, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);

        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            await _users.UpdateAsync(user, ct);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);

        var accessToken = _jwt.IssueAccessToken(user);
        var rawRefresh = GenerateSecureToken();
        var refreshHash = HashToken(rawRefresh);

        await _refreshTokens.CreateAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
            CreatedAt = DateTime.UtcNow,
            UserAgent = ctx.Request.Headers.UserAgent.ToString(),
            IpAddress = ctx.Connection.RemoteIpAddress?.ToString()
        }, ct);

        SetRefreshCookie(ctx, rawRefresh);

        return BuildLoginResponse(accessToken, user);
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken, HttpContext ctx, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, ct);

        if (stored is null || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        if (stored.RevokedAt.HasValue)
        {
            // Replay attack — revoke entire family.
            await _refreshTokens.RevokeAllForUserAsync(stored.UserId, ct);
            _logger.LogWarning("Refresh token replay detected for user {UserId}. All tokens revoked.", stored.UserId);
            throw new UnauthorizedAccessException("Refresh token has already been used. All sessions revoked.");
        }

        var user = await _users.GetByIdAsync(stored.UserId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        var rawNew = GenerateSecureToken();
        var hashNew = HashToken(rawNew);

        var newToken = await _refreshTokens.CreateAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hashNew,
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
            CreatedAt = DateTime.UtcNow,
            UserAgent = ctx.Request.Headers.UserAgent.ToString(),
            IpAddress = ctx.Connection.RemoteIpAddress?.ToString()
        }, ct);

        await _refreshTokens.RevokeAsync(stored.Id, newToken, ct);

        SetRefreshCookie(ctx, rawNew);

        var accessToken = _jwt.IssueAccessToken(user);
        return BuildLoginResponse(accessToken, user);
    }

    public async Task LogoutAsync(int userId, string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, ct);
        if (stored is not null && stored.UserId == userId)
            await _refreshTokens.RevokeAsync(stored.Id, null, ct);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        EnforcePasswordPolicy(request.NewPassword);

        if (_hasher.Verify(request.NewPassword, user.PasswordHash))
            throw new InvalidOperationException("New password must differ from the current password.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;
        await _users.UpdateAsync(user, ct);

        await _refreshTokens.RevokeAllForUserAsync(userId, ct);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null) return; // Do not leak existence.

        var rawToken = GenerateSecureToken();
        var hash = HashToken(rawToken);

        await _resetTokens.CreateAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime),
            CreatedAt = DateTime.UtcNow
        }, ct);

        // Phase 2 SMTP is out of scope — log the token for the admin to retrieve.
        _logger.LogInformation(
            "[PASSWORD_RESET] Reset link for {Email}: /auth/reset-password?token={Token}",
            email, rawToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var hash = HashToken(request.Token);
        var stored = await _resetTokens.GetByHashAsync(hash, ct)
            ?? throw new UnauthorizedAccessException("Reset token is invalid or expired.");

        if (stored.ExpiresAt < DateTime.UtcNow || stored.UsedAt.HasValue)
            throw new UnauthorizedAccessException("Reset token is invalid or expired.");

        var user = await _users.GetByIdAsync(stored.UserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        EnforcePasswordPolicy(request.NewPassword);

        if (_hasher.Verify(request.NewPassword, user.PasswordHash))
            throw new InvalidOperationException("New password must differ from the current password.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;
        await _users.UpdateAsync(user, ct);

        await _resetTokens.MarkUsedAsync(stored.Id, ct);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
    }

    private static void EnforcePasswordPolicy(string password)
    {
        if (password.Length < 12)
            throw new InvalidOperationException("Password must be at least 12 characters.");

        int categories = 0;
        if (password.Any(char.IsUpper)) categories++;
        if (password.Any(char.IsLower)) categories++;
        if (password.Any(char.IsDigit)) categories++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) categories++;

        if (categories < 3)
            throw new InvalidOperationException(
                "Password must contain at least 3 of: uppercase, lowercase, digit, symbol.");
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void SetRefreshCookie(HttpContext ctx, string rawToken)
    {
        ctx.Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = RefreshTokenLifetime
        });
    }

    private LoginResponse BuildLoginResponse(string accessToken, ControlItUser user) =>
        new(
            AccessToken: accessToken,
            ExpiresIn: _jwt.AccessTokenLifetimeSeconds,
            User: new UserSummary(
                Id: user.Id,
                Email: user.Email,
                Role: user.Role,
                TenantId: user.TenantId,
                MustChangePassword: user.MustChangePassword));
}
