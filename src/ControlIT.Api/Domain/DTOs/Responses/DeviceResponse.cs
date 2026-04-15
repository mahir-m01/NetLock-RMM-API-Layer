// DeviceResponse.cs — Response DTO for device data sent to the dashboard.
// Pattern: DTO (Data Transfer Object)
//
// WHY a separate DTO instead of returning Device directly:
// The Device model contains sensitive fields (AccessKey) that must not be exposed.
// A dedicated DTO controls the exact API contract and decouples it from the domain model.
//
// IsOnline is COMPUTED by ControlItFacade — it is not stored in the DB.
// It is true when LastAccess >= UtcNow - 5 minutes.

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

    // Current CPU usage percentage (0.0 - 100.0)
    public double CpuUsage { get; set; }

    // Current RAM usage percentage (0.0 - 100.0)
    public double RamUsage { get; set; }

    // Computed in ControlItFacade: true if LastAccess >= DateTime.UtcNow.AddMinutes(-5)
    // This field does NOT exist in the database — it's calculated per-request.
    public bool IsOnline { get; set; }

    // The raw LastAccess timestamp — let the dashboard decide how to display it
    public DateTime LastAccess { get; set; }
}
