namespace ControlIT.Api.Domain.Models;

public sealed record NetLockConnectedDevicesSnapshot(
    IReadOnlySet<string> ConnectedAccessKeys,
    bool IsDegraded,
    string? DegradedReason,
    DateTimeOffset ObservedAt);
