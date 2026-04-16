// DeviceResponse.cs — Response DTO for device data sent to the dashboard.
// Pattern: DTO (Data Transfer Object)
//
// WHY a separate DTO instead of returning Device directly:
// The Device model contains sensitive fields (AccessKey) that must not be exposed.
// A dedicated DTO controls the exact API contract and decouples it from the domain model.
//
// IsOnline is COMPUTED by ControlItFacade — it is not stored in the DB.
// It is true when LastAccess >= DateTime.Now.AddMinutes(-5).
//
// CpuUsage / RamUsage are nullable: when IsOnline = false the agent is not
// heartbeating so those values are stale/meaningless. Null tells the UI to
// show "—" rather than a misleading number.

namespace ControlIT.Api.Domain.DTOs.Responses;

/// <summary>
/// The device data shape returned by GET /devices and GET /devices/{id}.
/// AccessKey is intentionally omitted — never expose routing keys to the dashboard.
/// </summary>
public class DeviceResponse
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IpAddressInternal { get; set; } = string.Empty;

    // Null when IsOnline = false — stale agent data is not meaningful when offline.
    public double? CpuUsage { get; set; }

    // Null when IsOnline = false — stale agent data is not meaningful when offline.
    public double? RamUsage { get; set; }

    // Computed in ControlItFacade: true if LastAccess >= DateTime.Now.AddMinutes(-5).
    // This field does NOT exist in the database — it's calculated per-request.
    public bool IsOnline { get; set; }

    // The raw LastAccess timestamp — let the dashboard decide how to display it
    public DateTime LastAccess { get; set; }
}
