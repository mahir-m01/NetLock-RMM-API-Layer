using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ControlIT.Api.Infrastructure.Auth;

public sealed class JwtService : IJwtService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly ILogger<JwtService> _logger;
    private const string Issuer = "controlit-api";
    private const string Audience = "controlit-dashboard";

    public int AccessTokenLifetimeSeconds => 15 * 60;

    public JwtService(ILogger<JwtService> logger)
    {
        _logger = logger;
        var key = Environment.GetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY");
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            _logger.LogCritical(
                "CONTROLIT_JWT_SIGNING_KEY is missing or shorter than 32 bytes. Set it and restart.");
            throw new InvalidOperationException(
                "CONTROLIT_JWT_SIGNING_KEY must be set and at least 32 bytes (256 bits).");
        }
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public string IssueAccessToken(ControlItUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(user.AssignedClientsJson))
            claims.Add(new Claim("assigned_clients", user.AssignedClientsJson));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(AccessTokenLifetimeSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = _signingKey,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }

    public static TokenValidationParameters BuildValidationParameters(string signingKey) =>
        new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // Without these, JwtSecurityTokenHandler remaps "role" and "sub" to long
            // ClaimTypes URI forms (http://schemas.microsoft.com/.../claims/role).
            // Explicitly setting the short-form names keeps FindFirst("role") and
            // FindFirst("sub") working in HttpActorContext and policy assertions.
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
}
