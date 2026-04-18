using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.Interfaces;

public interface IActorContext
{
    int UserId { get; }
    Role Role { get; }
    int? TenantId { get; }
    IReadOnlyList<int> AssignedClients { get; }
    string? IpAddress { get; }
}
