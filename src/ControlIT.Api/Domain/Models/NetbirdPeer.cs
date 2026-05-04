// NetbirdPeer.cs — Domain model for a peer in the Netbird mesh VPN network.
// Populated by NetbirdApiClient from the Netbird Management API /api/peers endpoint.
// Netbird is the mesh VPN layer that allows ControlIT to reach managed devices
// even when they're behind NAT or firewalls.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Represents a device registered as a peer in the Netbird mesh network.
/// Mapped from Netbird API JSON responses — not from MySQL.
/// </summary>
public class NetbirdPeer
{
    // Netbird's internal peer ID (UUID string)
    public string Id { get; set; } = string.Empty;

    // The hostname/label of the peer in Netbird
    public string Name { get; set; } = string.Empty;

    // The Wireguard/mesh IP assigned to this peer within the Netbird network
    public string Ip { get; set; } = string.Empty;

    // Operating system string from Netbird's peer info
    public string Os { get; set; } = string.Empty;

    // Whether this peer is currently connected to the Netbird network
    public bool Connected { get; set; }

    // Timestamp of the last connection/heartbeat from this peer
    public DateTime LastSeen { get; set; }

    public string Hostname { get; set; } = string.Empty;
    public string DnsLabel { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string ConnectionIp { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public bool SshEnabled { get; set; }
    public bool LoginExpired { get; set; }
    public bool LoginExpirationEnabled { get; set; }
    public int AccessiblePeersCount { get; set; }
    public List<NetbirdPeerGroup> Groups { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class NetbirdPeerGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
