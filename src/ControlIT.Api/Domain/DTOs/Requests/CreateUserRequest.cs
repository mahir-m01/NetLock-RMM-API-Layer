using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.DTOs.Requests;

public sealed record CreateUserRequest(
    string Email,
    Role Role,
    int? TenantId,
    IReadOnlyList<int>? AssignedClients);
