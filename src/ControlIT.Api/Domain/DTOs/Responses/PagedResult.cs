// PagedResult.cs — Generic paginated response wrapper.
//
// WHY a generic wrapper instead of one-off classes per endpoint:
// Every list endpoint returns the same pagination metadata (total, page, size, totalPages).
// A single generic type provides type-safe pagination for devices, events, audits, etc.

namespace ControlIT.Api.Domain.DTOs.Responses;

/// <summary>
/// Wraps any paginated list response with pagination metadata.
/// Returned by GET /devices, GET /events, GET /audit/logs, etc.
/// </summary>
public class PagedResult<T>
{
    // The actual data items for this page
    public IEnumerable<T> Items { get; set; } = [];

    // Total number of matching records across ALL pages (not just this page)
    public int TotalCount { get; set; }

    // The current page number (1-indexed)
    public int Page { get; set; }

    // How many items per page
    public int PageSize { get; set; }

    // Computed from TotalCount and PageSize. Math.Ceiling ensures partial pages are counted
    // (e.g., 101 items at 25 per page = 5 pages).
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
