// Device.cs — Domain model representing a managed endpoint in NetLock's database.
// Pattern: Domain Model (anemic, since NetLock owns this data — no behavior here)
//
// WHY anemic here:
// ControlIT reads this data but doesn't own it. NetLock writes and manages device records.
// Our job is to READ and PRESENT, not to mutate. Rich domain methods would be inappropriate.
//
// ORM mapping:
// This class is mapped by Dapper (NOT EF Core). Dapper reads rows from the `devices` table
// and maps column names to property names via SqlMapper.Settings.MatchNamesWithUnderscores = true.
// That setting (enabled in Program.cs) automatically maps:
//   device_name  → DeviceName
//   tenant_id    → TenantId
//   last_access  → LastAccess  ... and so on.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Represents a managed endpoint device from NetLock's `devices` table.
/// Mapped by Dapper. Read-only from ControlIT's perspective.
/// </summary>
public class Device
{
    // Primary key — INTEGER in MySQL, maps to int in C#
    public int Id { get; set; }

    // Foreign key to the tenants table — used for all tenant-scoped queries
    public int TenantId { get; set; }

    // Foreign key to the locations table — physical/logical grouping of devices
    public int LocationId { get; set; }

    // The human-readable device name (hostname or custom label)
    public string DeviceName { get; set; } = string.Empty;

    // The unique access key used to identify and communicate with the device via SignalR
    // This is what ControlIT uses as the routing key when dispatching remote commands
    public string AccessKey { get; set; } = string.Empty;

    // OS platform: "Windows", "Linux", "macOS"
    public string Platform { get; set; } = string.Empty;

    // Full OS version string (e.g., "Windows 11 22H2", "Ubuntu 22.04 LTS")
    public string OperatingSystem { get; set; } = string.Empty;

    // Version of the NetLock agent installed on this device
    public string AgentVersion { get; set; } = string.Empty;

    // CPU model string (e.g., "Intel Core i7-12700K")
    public string Cpu { get; set; } = string.Empty;

    // Current CPU usage percentage (0.0 - 100.0)
    public double CpuUsage { get; set; }

    // RAM capacity string (e.g., "16 GB")
    public string Ram { get; set; } = string.Empty;

    // Current RAM usage percentage (0.0 - 100.0)
    public double RamUsage { get; set; }

    // Internal (LAN) IP address of the device
    public string IpAddressInternal { get; set; } = string.Empty;

    // External (WAN/public) IP address of the device
    public string IpAddressExternal { get; set; } = string.Empty;

    // Timestamp of the last heartbeat/check-in from the agent
    // Used to determine if a device is "online" (last_access within the last 5 minutes)
    public DateTime LastAccess { get; set; }

    // Whether the device has been authorized by an admin in NetLock
    public bool Authorized { get; set; }

    // Whether the device's configuration is synced with the server
    public bool Synced { get; set; }
}
