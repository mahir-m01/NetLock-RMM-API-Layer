// ─────────────────────────────────────────────────────────────────────────────
// TenantTargetResolver.cs
// Single-purpose resolver for tenant target scoping on network endpoints.
//
// Problem: SuperAdmin/CpAdmin have null TenantId in their JWT (cross-tenant).
// They must supply ?targetTenantId=N so the backend can scope the operation.
// Scoped roles (ClientAdmin, Technician) are locked to their own TenantId and
// must never be permitted to override it.
//
// Usage: call ResolveAsync and handle the result before proceeding with any
// tenant-scoped operation.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application;

using ControlIT.Api.Domain.Interfaces;

/// <summary>
/// Discriminated result returned by TenantTargetResolver.ResolveAsync.
/// Exactly one of <see cref="TenantId"/> or <see cref="Error"/> is populated.
/// </summary>
public sealed class TenantResolutionResult
{
    public int? TenantId { get; private init; }
    public string? Error { get; private init; }
    public int StatusCode { get; private init; }

    public bool IsSuccess => TenantId.HasValue;

    public static TenantResolutionResult Success(int tenantId) =>
        new() { TenantId = tenantId, StatusCode = 200 };

    public static TenantResolutionResult Fail(string error, int statusCode) =>
        new() { Error = error, StatusCode = statusCode };
}

/// <summary>
/// Resolves the effective tenant ID for a network operation from the caller's
/// TenantContext and an optional ?targetTenantId query parameter.
///
/// Rules:
///   Elevated role (IsAllTenants == true):
///     - No targetTenantId  → 400 "targetTenantId is required for elevated roles"
///     - targetTenantId not found in DB → 400 "Tenant {id} not found"
///     - targetTenantId valid → resolved to that tenant ID
///
///   Scoped role (IsAllTenants == false):
///     - !IsResolved → 400 "No tenant context available"
///     - targetTenantId supplied AND != own TenantId → 403 "Cross-tenant access denied"
///     - otherwise → resolved to own TenantId
/// </summary>
public static class TenantTargetResolver
{
    public static async Task<TenantResolutionResult> ResolveAsync(
        TenantContext tenant,
        int? targetTenantId,
        ITenantRepository tenants,
        CancellationToken ct = default)
    {
        if (tenant.IsAllTenants)
            return await ResolveElevatedAsync(targetTenantId, tenants, ct);

        return ResolveScopedRole(tenant, targetTenantId);
    }

    private static async Task<TenantResolutionResult> ResolveElevatedAsync(
        int? targetTenantId,
        ITenantRepository tenants,
        CancellationToken ct)
    {
        if (targetTenantId is null)
            return TenantResolutionResult.Fail(
                "targetTenantId is required for elevated roles", 400);

        var found = await tenants.GetByIdAsync(targetTenantId.Value, ct);
        if (found is null)
            return TenantResolutionResult.Fail(
                $"Tenant {targetTenantId.Value} not found", 400);

        return TenantResolutionResult.Success(targetTenantId.Value);
    }

    private static TenantResolutionResult ResolveScopedRole(
        TenantContext tenant,
        int? targetTenantId)
    {
        if (!tenant.IsResolved)
            return TenantResolutionResult.Fail("No tenant context available", 400);

        if (targetTenantId.HasValue && targetTenantId.Value != tenant.TenantId!.Value)
            return TenantResolutionResult.Fail("Cross-tenant access denied", 403);

        return TenantResolutionResult.Success(tenant.TenantId!.Value);
    }
}
