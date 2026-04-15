// DeviceEvent.cs — Domain model for security/operational events from managed devices.
// Mapped by Dapper from the `events` table.
//
// CRITICAL MAPPING NOTE — READ BEFORE TOUCHING THE REPOSITORY:
// The events table has TWO non-standard column names that CANNOT be auto-mapped by Dapper:
//
//   1. Column `_event` → maps to property `Event`
//      MatchNamesWithUnderscores handles word separator underscores (device_name → DeviceName)
//      but does NOT handle a leading underscore prefix. So `_event` would try to map to nothing.
//      Every query MUST include: `e._event AS Event`
//
//   2. Column `date` → maps to property `Timestamp`
//      The column name in MySQL is literally "date", but our C# property is "Timestamp"
//      (more descriptive). Every query MUST include: `e.date AS Timestamp`
//
// If you forget these aliases, Dapper silently leaves Event="" and Timestamp=default(DateTime).
// No exception is thrown. You won't notice until runtime when data looks wrong.

namespace ControlIT.Api.Domain.Models;

/// <summary>
/// Represents a security/operational event from NetLock's `events` table.
/// Mapped by Dapper with explicit column aliases for _event and date columns.
/// </summary>
public class DeviceEvent
{
    // Primary key
    public int Id { get; set; }

    // Foreign key to the devices table — which device generated this event
    public int DeviceId { get; set; }

    // Denormalized tenant name (events table stores tenant name, not tenant_id).
    // This is why queries join to the tenants table to filter by tenant_id.
    public string TenantName { get; set; } = string.Empty;

    // Denormalized device name at the time of the event
    public string DeviceName { get; set; } = string.Empty;

    // Mapped from `date` column via explicit SQL alias: `e.date AS Timestamp`
    // The column name is "date" in MySQL — renamed here for clarity.
    public DateTime Timestamp { get; set; }

    // Event severity level: "INFO", "WARNING", "CRITICAL", etc.
    public string Severity { get; set; } = string.Empty;

    // The NetLock component that reported this event (e.g., "agent", "server")
    public string ReportedBy { get; set; } = string.Empty;

    // Mapped from `_event` column via explicit SQL alias: `e._event AS Event`
    // Leading underscore in column name breaks Dapper's auto-mapping — must alias every time.
    public string Event { get; set; } = string.Empty;

    // Detailed description of what happened
    public string Description { get; set; } = string.Empty;
}
