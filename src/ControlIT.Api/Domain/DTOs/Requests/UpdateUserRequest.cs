using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.DTOs.Requests;

public sealed record UpdateUserRequest(
    Role? Role,
    int? TenantId,
    IReadOnlyList<int>? AssignedClients,
    bool? IsActive);
