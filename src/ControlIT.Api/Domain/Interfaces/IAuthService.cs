using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using Microsoft.AspNetCore.Http;

namespace ControlIT.Api.Domain.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext ctx, CancellationToken ct = default);
    Task<LoginResponse> RefreshAsync(string refreshToken, HttpContext ctx, CancellationToken ct = default);
    Task LogoutAsync(int userId, string refreshToken, CancellationToken ct = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
