// ─────────────────────────────────────────────────────────────────────────────
// EventResponse.cs
// DTO (Data Transfer Object) — the shape of event data returned by the API.
// This is a trimmed version of DeviceEvent; we don't expose internal IDs like
// device_id or tenant_name_snapshot to external callers.
//
// WHY: Never return the raw domain model directly from an API. DTOs let you
// control exactly what fields are serialized to JSON, add computed fields
// (IsOnline on DeviceResponse), and change the internal model without
// breaking the API contract.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.DTOs.Responses;

public class EventResponse
{
    public int Id { get; set; }

    // The name of the device that generated this event.
    public string DeviceName { get; set; } = string.Empty;

    // Severity level string (e.g., "Critical", "Warning", "Info").
    public string Severity { get; set; } = string.Empty;

    // The event type/name. Mapped from the `_event` column via SQL alias `_event AS Event`.
    // The leading underscore in the column name is a NetLock convention, not a word separator.
    public string Event { get; set; } = string.Empty;

    // Human-readable description of what happened.
    public string Description { get; set; } = string.Empty;

    // When the event occurred. Mapped from the `date` column via SQL alias `date AS Timestamp`.
    public DateTime Timestamp { get; set; }
}
