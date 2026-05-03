namespace ControlIT.Api.Infrastructure.NetLock;

using Dapper;
using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public sealed class NetLockAdminSessionTokenProvider : INetLockAdminSessionTokenProvider
{
    private const string CacheKey = "netlock:admin_session_token";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IDbConnectionFactory _factory;
    private readonly NetLockOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NetLockAdminSessionTokenProvider> _logger;

    public NetLockAdminSessionTokenProvider(
        IDbConnectionFactory factory,
        IOptions<NetLockOptions> options,
        IMemoryCache cache,
        ILogger<NetLockAdminSessionTokenProvider> logger)
    {
        _factory = factory;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        var token = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await ReadAdministratorTokenAsync(ct);
        });

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("NetLock administrator session token is unavailable.");

        return token;
    }

    private async Task<string> ReadAdministratorTokenAsync(CancellationToken ct)
    {
        try
        {
            using var conn = await _factory.CreateConnectionAsync(ct);
            var token = await conn.ExecuteScalarAsync<string?>(
                """
                SELECT remote_session_token
                FROM accounts
                WHERE role = 'Administrator'
                  AND remote_session_token IS NOT NULL
                  AND remote_session_token <> ''
                ORDER BY id
                LIMIT 1
                """);

            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read NetLock administrator session token from accounts table. Falling back to configured token.");
        }

        if (!string.IsNullOrWhiteSpace(_options.AdminSessionToken))
            return _options.AdminSessionToken;

        throw new InvalidOperationException("NetLock administrator session token is not configured.");
    }
}
