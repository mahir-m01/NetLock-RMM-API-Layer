using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Application;

/// <summary>
/// Scoped per-request ambient context carrying the authenticated user's tenant scope.
/// Populated from JWT claims via IActorContext — not from API keys.
/// </summary>
public class TenantContext
{
    private readonly IActorContext _actor;

    public TenantContext(IActorContext actor) => _actor = actor;

    /// <summary>
    /// The authenticated user's tenant ID, or null for SuperAdmin/CpAdmin users
    /// who have cross-tenant access.
    /// </summary>
    public int? TenantId => _actor.TenantId;

    /// <summary>
    /// True when the caller has access to all tenants (SuperAdmin or CpAdmin).
    /// Repositories must not filter by tenant when this is true.
    /// </summary>
    public bool IsAllTenants => _actor.Role is Role.SuperAdmin or Role.CpAdmin;

    /// <summary>
    /// True when the tenant scope has been resolved from a valid JWT.
    /// Always true when IActorContext is populated correctly by the auth middleware.
    /// </summary>
    public bool IsResolved => IsAllTenants || TenantId.HasValue;
}
