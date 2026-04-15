// PagedResult.cs — Generic paginated response wrapper.
// Pattern: Generic type (TypeScript generics work the same way: PagedResult<T>)
//
// WHY a generic wrapper instead of a one-off class per endpoint:
// Every list endpoint returns the same pagination metadata (total, page, size, totalPages).
// By making this generic, we get type-safe pagination for devices, events, audits, etc.
// In TypeScript: interface PagedResult<T> { items: T[]; totalCount: number; ... }

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

    // Computed property: how many pages exist in total.
    // Math.Ceiling ensures partial pages are counted (e.g., 101 items / 25 per page = 5 pages)
    // 'get' means this is read-only and computed on access — like a TypeScript getter.
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
