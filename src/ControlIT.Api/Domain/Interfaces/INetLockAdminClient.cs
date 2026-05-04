// INetLockAdminClient.cs — Contract for reading real-time device connection state from NetLock.
//
// WHY this exists:
// NetLock tracks connected devices in memory (CommandHubSingleton._clientConnections),
// not in the database. The /admin/devices/connected endpoint is the only way to read
// that state. last_access is a heartbeat timestamp — it lags up to 5 minutes behind
// real connection state. Using it causes the "showed online after shutdown" bug.
//
// This client calls NetLock's own endpoint — exactly what NetLock's web console does.

namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.Models;

public interface INetLockAdminClient
{
    /// <summary>
    /// Returns the set of device access_keys currently connected to NetLock's SignalR hub.
    /// Source: GET /admin/devices/connected on the NetLock server.
    /// A device is online iff its access_key is in this set.
    /// </summary>
    Task<IReadOnlySet<string>> GetConnectedAccessKeysAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a fresh live snapshot plus degraded status for push bridge health.
    /// Access keys stay internal and must never be serialized to clients.
    /// </summary>
    Task<NetLockConnectedDevicesSnapshot> GetConnectedDevicesSnapshotAsync(
        CancellationToken ct = default);
}
