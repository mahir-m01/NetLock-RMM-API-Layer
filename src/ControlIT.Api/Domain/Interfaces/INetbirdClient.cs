// ─────────────────────────────────────────────────────────────────────────────
// INetbirdClient.cs
// Pattern: Adapter — wraps the Netbird HTTP API behind a typed interface.
// NetbirdApiClient is the concrete adapter; this interface is what the
// endpoints and application layer depend on.
//
// WHY: If Netbird changes their API or we switch mesh VPN providers, only
// NetbirdApiClient needs to change. All callers depend on this interface,
// not on HttpClient or Netbird-specific details.
//
// Authorization note: Netbird uses "Authorization: Token <TOKEN>" — NOT Bearer.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

using ControlIT.Api.Domain.Models;

public interface INetbirdClient
{
    // Returns all peers registered in the Netbird management server.
    Task<IEnumerable<NetbirdPeer>> GetPeersAsync(CancellationToken cancellationToken = default);

    // Returns a specific peer by its Netbird peer ID, or null if not found.
    Task<NetbirdPeer?> GetPeerByIdAsync(string peerId, CancellationToken cancellationToken = default);

    // Enrols a new peer using a setup key. The peer must call home separately;
    // this just registers the key with the management server.
    Task EnrolPeerAsync(string setupKey, CancellationToken cancellationToken = default);

    // Removes a peer from the mesh network. The device will be disconnected.
    Task RemovePeerAsync(string peerId, CancellationToken cancellationToken = default);
}
