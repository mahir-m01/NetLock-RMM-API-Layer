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

    // IDbConnectionFactory is injected into the constructor because ApiKeyMiddleware
    // is a Singleton (middleware classes are singletons in ASP.NET Core).
    // We can't inject Scoped services directly into Singleton constructors — that's the
    // "captive dependency" bug. IDbConnectionFactory is also Singleton, so this is safe.
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    // --- In-memory cache ---
    // 5-minute TTL cache: avoids a DB query on every single request.
    // After the first successful auth, each keyHash→(TenantId, Expiry) mapping is cached for 5 minutes.
    // Using ConcurrentDictionary so every distinct API key gets its own entry — no single-entry
    // bottleneck where two clients with different keys would evict each other's cached result.
    // ConcurrentDictionary is inherently thread-safe; no lock is needed anywhere.
    // This is per-process memory (not distributed) — works fine for a single-instance API.
    private readonly ConcurrentDictionary<string, (int TenantId, DateTime Expiry)> _cache = new();

    public ApiKeyMiddleware(RequestDelegate next, IDbConnectionFactory factory,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// InvokeAsync receives a TenantContext via parameter injection — NOT constructor injection.
    /// This is how ASP.NET Core middleware accesses Scoped services: through method parameters.
    /// Each request gets a fresh TenantContext instance (Scoped = one per request).
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

        // TryGetValue returns false if the header is absent — prevents KeyNotFoundException.
        // In TypeScript: const rawKey = req.headers['x-api-key']
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

        // TryGetValue is atomic on ConcurrentDictionary — no lock required.
        // Each API key maps to its own entry, so concurrent requests from different tenants
        // never interfere with each other's cached result.
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

            // Update the in-memory cache for the next 5 minutes.
            // ConcurrentDictionary indexer assignment is thread-safe — no lock required.
            _cache[keyHash] = (tenantId.Value, DateTime.UtcNow.AddMinutes(5));
        }

        // CRITICAL: Set TenantContext.TenantId from the DB-derived value ONLY.
        // This is the ONLY place TenantId is set in the entire application.
        // All repositories read from this context object — never from request parameters.
        tenantContext.TenantId = tenantId.Value;

        // Pass control to the next middleware (rate limiter, then endpoint handler).
        await _next(context);
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string and returns it as lowercase hex.
    /// This is the same algorithm used when seeding API keys into the database.
    /// In Node.js: crypto.createHash('sha256').update(input).digest('hex')
    /// </summary>
    private static string ComputeSha256(string input)
    {
        // SHA256.HashData: one-shot hash computation (no need to manage a HashAlgorithm instance)
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));

        // Convert.ToHexString: bytes → uppercase hex string
        // .ToLowerInvariant(): normalize to lowercase for consistent comparison
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
