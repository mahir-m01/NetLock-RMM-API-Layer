// TenantContext.cs — Per-request tenant scope carrier.
// Pattern: Ambient Context / Request-Scoped State
//
// WHY this exists:
// Every repository query must be filtered to the current tenant's data.
// Rather than threading tenantId through every method signature, a Scoped DI object
// is set once by ApiKeyMiddleware and read by any service that needs it.
//
// LIFETIME: Scoped — one instance per HTTP request. Discarded when the request ends.
// ApiKeyMiddleware sets TenantId once at the start of the pipeline.
// Repositories, services, and the facade READ from it but never SET it.
//
// SECURITY: TenantId is set EXCLUSIVELY inside ApiKeyMiddleware from a database lookup.
// No code outside ApiKeyMiddleware should ever assign TenantContext.TenantId.

namespace ControlIT.Api.Application;

/// <summary>
/// Scoped per-request ambient context carrying the authenticated tenant's ID.
/// Set exclusively by ApiKeyMiddleware from the API key DB lookup.
/// Never set from request parameters, headers, body, or query strings.
/// </summary>
public class TenantContext
{
    /// <summary>
    /// The database ID of the authenticated tenant.
    /// Default 0 = unresolved (ApiKeyMiddleware hasn't run yet or was bypassed).
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// True when TenantId has been set from a successful API key lookup.
    /// Repositories should guard: if (!tenantContext.IsResolved) throw InvalidOperationException.
    /// This prevents accidental cross-tenant data leaks if middleware is accidentally skipped.
    /// </summary>
    public bool IsResolved => TenantId > 0;
}
