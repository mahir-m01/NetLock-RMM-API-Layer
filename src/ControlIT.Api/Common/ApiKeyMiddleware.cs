// ApiKeyMiddleware.cs — Authentication + tenant derivation middleware.
// Pattern: Middleware (ASP.NET Core pipeline interceptor)
//
// SECURITY DESIGN (critical to understand):
// The tenant ID is NEVER read from the request (no header, no query param, no body).
// Instead, the raw API key is hashed with SHA-256 and looked up in controlit_tenant_api_keys.
// The tenant_id comes ONLY from the database row that matches the key hash.
// This ensures a client cannot impersonate another tenant by sending a different tenant_id.
//
// Flow:
// 1. Read raw key from x-api-key header
// 2. Hash it with SHA-256 (lowercase hex)
// 3. Check 5-minute in-memory cache (avoids DB hit on every request)
// 4. On cache miss: query controlit_tenant_api_keys WHERE key_hash = @hash AND not expired
// 5. Set TenantContext.TenantId from the DB result
// 6. Continue to next middleware

namespace ControlIT.Api.Common;

using System.Collections.Concurrent;
using Dapper;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;

/// <summary>
/// Validates the x-api-key header and derives the tenant_id from the database.
/// Populates TenantContext.TenantId — the ONLY place this value is set.
/// Exempts /health paths from authentication.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    // Middleware classes are Singleton in ASP.NET Core. IDbConnectionFactory is also Singleton,
    // so constructor injection here is safe — no captive dependency risk.
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    // --- In-memory cache ---
    // 5-minute TTL cache — avoids a DB hit on every request after the first successful auth.
    // Keyed by key hash so distinct API keys never evict each other's cached result.
    // ConcurrentDictionary provides thread-safe reads and writes without explicit locking.
    // Per-process only — suitable for a single-instance deployment.
    private readonly ConcurrentDictionary<string, (int TenantId, DateTime Expiry)> _cache = new();

    public ApiKeyMiddleware(RequestDelegate next, IDbConnectionFactory factory,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// TenantContext is injected via method parameter rather than constructor injection
    /// because it is Scoped — method-level injection is how ASP.NET Core middleware
    /// accesses Scoped services from a Singleton class.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // /health is exempt — monitoring tools must be able to check health without auth.
        // StartsWithSegments does a path-safe prefix match (handles /health, /health/live, etc.)
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // TryGetValue returns false when the header is absent, avoiding a KeyNotFoundException.
        if (!context.Request.Headers.TryGetValue("x-api-key", out var rawKey)
            || string.IsNullOrWhiteSpace(rawKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key required." });
            return;
        }

        // Hash the raw key before any DB operation — we never store the plaintext key.
        // The database only stores SHA-256 hashes, so we compare hash-to-hash.
        var keyHash = ComputeSha256(rawKey!);

        // --- Cache check (thread-safe) ---
        int? tenantId = null;

        if (_cache.TryGetValue(keyHash, out var cached) && DateTime.UtcNow < cached.Expiry)
            tenantId = cached.TenantId;

        if (tenantId is null)
        {
            // Cache miss or expired — query the database.
            // Table: controlit_tenant_api_keys
            // Columns: key_hash (string), tenant_id (int), expires_at (datetime, nullable)
            // NOTE: Column is key_hash, NOT api_key_hash. Column expires_at, NOT is_active.
            using var conn = await _factory.CreateConnectionAsync();
            tenantId = await conn.ExecuteScalarAsync<int?>(
                @"SELECT tenant_id FROM controlit_tenant_api_keys
                  WHERE key_hash = @keyHash
                  AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP())",
                new { keyHash });

            if (tenantId is null)
            {
                // Key not found or expired — log the first 8 chars of the hash (safe to log,
                // not the raw key) to help with debugging without leaking the full hash.
                _logger.LogWarning("Invalid or expired API key. Hash={Hash}", keyHash[..8]);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
                return;
            }

            // Cache the result for 5 minutes.
            _cache[keyHash] = (tenantId.Value, DateTime.UtcNow.AddMinutes(5));
        }

        // NOTE: ApiKeyMiddleware is retired in Contract 05B. TenantContext now derives
        // from JWT claims via IActorContext/HttpActorContext. This file is retained for
        // rollback reference only — it is NOT registered in the pipeline.

        // Pass control to the next middleware (rate limiter, then endpoint handler).
        await _next(context);
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string and returns it as lowercase hex.
    /// Matches the algorithm used when seeding API keys into the database.
    /// </summary>
    private static string ComputeSha256(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        // ToLowerInvariant normalises to lowercase for consistent comparison.
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
