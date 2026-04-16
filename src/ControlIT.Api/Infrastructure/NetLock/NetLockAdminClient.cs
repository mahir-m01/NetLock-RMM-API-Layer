// ─────────────────────────────────────────────────────────────────────────────
// NetLockAdminClient.cs
// Calls NetLock's /admin/devices/connected endpoint to get real-time connection
// state from CommandHubSingleton._clientConnections.
//
// Authentication: NetLock's own x-api-key (AdminSessionToken from config).
// Base URL: derived from HubUrl by stripping the SignalR path component.
//   e.g. "http://localhost:7080/commandHub" → "http://localhost:7080"
//
// WHY an HTTP client instead of reading from memory:
// NetLock runs as a separate process. Its in-memory hub state is not accessible
// from ControlIT's process. The only IPC surface is NetLock's own REST endpoint.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using System.Text.Json;
using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;

public class NetLockAdminClient : INetLockAdminClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly ILogger<NetLockAdminClient> _logger;

    public NetLockAdminClient(
        IHttpClientFactory httpFactory,
        IOptions<NetLockOptions> opts,
        ILogger<NetLockAdminClient> logger)
    {
        _http   = httpFactory.CreateClient("netlockadmin");
        _logger = logger;

        // Derive base URL from HubUrl by stripping the path.
        // "http://host:7080/commandHub" → "http://host:7080"
        var hubUri = new Uri(opts.Value.HubUrl);
        _baseUrl = hubUri.GetLeftPart(UriPartial.Authority);

        // Pre-set the API key header using FilesApiKey (from NetLock's settings table).
        // This is NOT the same as AdminSessionToken (remote_session_token for SignalR) —
        // the admin HTTP endpoints validate against settings.files_api_key.
        _http.DefaultRequestHeaders.Add("x-api-key", opts.Value.FilesApiKey);
    }

    public async Task<IReadOnlySet<string>> GetConnectedAccessKeysAsync(
        CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync($"{_baseUrl}/admin/devices/connected", ct);
            res.EnsureSuccessStatusCode();

            // Response: { "access_keys": ["key1", "key2", ...] }
            using var doc = await JsonDocument.ParseAsync(
                await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (doc.RootElement.TryGetProperty("access_keys", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var k = el.GetString();
                    if (!string.IsNullOrEmpty(k)) keys.Add(k);
                }
            }

            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "NetLockAdminClient: failed to fetch connected devices from {BaseUrl}. Treating all devices as offline.",
                _baseUrl);
            // Return empty set — facade will mark all devices offline rather than crash.
            return new HashSet<string>();
        }
    }
}
