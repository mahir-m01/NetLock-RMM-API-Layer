// ─────────────────────────────────────────────────────────────────────────────
// NetbirdApiClient.cs
// Pattern: Adapter — adapts the Netbird HTTP API to the INetbirdClient interface.
//
// WHY plain HttpClient (no Netbird SDK): No Netbird .NET library exists.
// We talk to their REST API directly. This keeps the dependency footprint small
// and means a Netbird API version change only requires updating this file.
//
// CRITICAL AUTH DETAIL: Netbird uses "Authorization: Token <TOKEN>" — NOT Bearer.
// Using "Bearer" instead causes a 401 with no useful error message from Netbird.
// This is set in the constructor via DefaultRequestHeaders.Authorization.
//
// HttpClient lifetime: Registered with AddHttpClient<INetbirdClient, NetbirdApiClient>()
// which uses the HttpClientFactory. This avoids socket exhaustion from creating
// new HttpClient instances per request (a common C# mistake).
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Netbird;

using System.Net.Http.Headers;
using System.Text.Json;
using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Options;

public class NetbirdApiClient : INetbirdClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NetbirdApiClient> _logger;

    // HttpClient is injected by IHttpClientFactory (via AddHttpClient<> in Program.cs).
    // IOptions<NetbirdOptions> gives us the base URL and token from appsettings.
    public NetbirdApiClient(HttpClient http,
        IOptions<NetbirdOptions> options,
        ILogger<NetbirdApiClient> logger)
    {
        _http = http;
        _logger = logger;

        // Set the base address so we can use relative paths in each method.
        // new Uri("https://example.com") — the trailing slash matters for relative resolution.
        _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");

        // CRITICAL: "Token" not "Bearer" — Netbird's API won't accept Bearer tokens.
        // AuthenticationHeaderValue("Token", value) produces: Authorization: Token <value>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", options.Value.Token);
    }

    public async Task<IEnumerable<NetbirdPeer>> GetPeersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("/api/peers", cancellationToken);
        response.EnsureSuccessStatusCode();  // Throws HttpRequestException on 4xx/5xx

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        // PropertyNameCaseInsensitive = true maps JSON "name" → C# "Name", "id" → "Id", etc.
        // The ?? [] means "return empty list if deserialization returns null".
        return JsonSerializer.Deserialize<IEnumerable<NetbirdPeer>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];
    }

    public async Task<NetbirdPeer?> GetPeerByIdAsync(string peerId,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"/api/peers/{peerId}", cancellationToken);

        // 404 = peer not found — return null instead of throwing.
        // Callers use the null return to send a 404 from the endpoint.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<NetbirdPeer>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task EnrolPeerAsync(string setupKey,
        CancellationToken cancellationToken = default)
    {
        // POST /api/peers with { "setup_key": "<key>" }
        // StringContent wraps the JSON string with the correct Content-Type header.
        var payload = new StringContent(
            JsonSerializer.Serialize(new { setup_key = setupKey }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync("/api/peers", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemovePeerAsync(string peerId,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"/api/peers/{peerId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
