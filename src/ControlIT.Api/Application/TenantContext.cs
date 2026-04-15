// TenantContext.cs — Per-request tenant scope carrier.
// Pattern: Ambient Context / Request-Scoped State
//
// WHY this exists:
// Every repository query must be filtered to the current tenant's data.
// Instead of passing tenantId as a parameter to every function (verbose and error-prone),
// we use a Scoped DI object that is set once by ApiKeyMiddleware and read by any service
// that needs it. This is similar to using React Context or AsyncLocalStorage in TypeScript.
//
// LIFETIME: Scoped — one instance per HTTP request. After the request ends, it's discarded.
// ApiKeyMiddleware sets TenantId once at the start of each request pipeline.
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
