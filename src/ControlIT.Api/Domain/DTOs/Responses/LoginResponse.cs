using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.DTOs.Responses;

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    UserSummary User);

public sealed record UserSummary(
    int Id,
    string Email,
    Role Role,
    int? TenantId,
    bool MustChangePassword);
