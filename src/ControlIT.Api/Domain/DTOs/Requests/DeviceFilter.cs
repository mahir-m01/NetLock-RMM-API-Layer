// DeviceFilter.cs — Query parameters for GET /devices.
// [AsParameters] in the endpoint handler instructs ASP.NET Core to bind
// query string parameters to this class's properties automatically.

namespace ControlIT.Api.Domain.DTOs.Requests;

/// <summary>
/// Query parameters for the GET /devices endpoint.
/// Bound via [AsParameters] in the Minimal API endpoint handler.
/// </summary>
public class DeviceFilter
{
    // Optional: filter by platform string ("Windows", "Linux", "macOS")
    public string? Platform { get; set; }

    // Optional: when true, only return devices that checked in within the last 5 minutes.
    public bool? OnlineOnly { get; set; }

    // Optional: filter devices by name (SQL LIKE %term% search)
    public string? SearchTerm { get; set; }

    // Pagination: which page to return (1-indexed). Default = page 1.
    public int Page { get; set; } = 1;

    // Pagination: how many records per page. Default = 25.
    // The repository enforces LIMIT and OFFSET from these values.
    public int PageSize { get; set; } = 25;
}
