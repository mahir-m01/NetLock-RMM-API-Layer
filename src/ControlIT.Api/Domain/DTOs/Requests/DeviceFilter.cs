// DeviceFilter.cs — Query parameters for GET /devices.
// [AsParameters] in the endpoint handler tells ASP.NET Core to bind query string params
// to this class's properties automatically.
//
// In TypeScript/Express, you'd do: const { platform, onlineOnly, page } = req.query
// In ASP.NET Core Minimal APIs: [AsParameters] DeviceFilter filter does the same thing.

namespace ControlIT.Api.Domain.DTOs.Requests;

/// <summary>
/// Query parameters for the GET /devices endpoint.
/// Bound via [AsParameters] in the Minimal API endpoint handler.
/// </summary>
public class DeviceFilter
{
    // Optional: filter by platform string ("Windows", "Linux", "macOS")
    public string? Platform { get; set; }

    // Optional: if true, only return devices that checked in within the last 5 minutes
    // '?' on bool means "nullable boolean" — in TypeScript: boolean | undefined
    public bool? OnlineOnly { get; set; }

    // Optional: filter devices by name (SQL LIKE %term% search)
    public string? SearchTerm { get; set; }

    // Pagination: which page to return (1-indexed). Default = page 1.
    public int Page { get; set; } = 1;

    // Pagination: how many records per page. Default = 25.
    // The repository enforces LIMIT and OFFSET from these values.
    public int PageSize { get; set; } = 25;
}
