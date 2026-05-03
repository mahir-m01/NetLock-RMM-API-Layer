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

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.Options;

public class NetbirdApiClient : INetbirdClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NetbirdApiClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public NetbirdApiClient(HttpClient http,
        IOptions<NetbirdOptions> options,
        ILogger<NetbirdApiClient> logger)
    {
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", options.Value.Token);
    }

    // ── Peers ──

    public async Task<IEnumerable<NetbirdPeer>> GetPeersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("/api/peers", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<IEnumerable<NetbirdPeer>>(json, _jsonOptions) ?? [];
    }

    public async Task<NetbirdPeer?> GetPeerByIdAsync(string peerId,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"/api/peers/{peerId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<NetbirdPeer>(json, _jsonOptions);
    }

    public async Task EnrolPeerAsync(string setupKey,
        CancellationToken cancellationToken = default)
    {
        var payload = new StringContent(
            JsonSerializer.Serialize(new { setup_key = setupKey }, _jsonOptions),
            Encoding.UTF8,
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

    public async Task<NetbirdPeer> UpdatePeerAsync(string peerId,
        UpdatePeerRequest request, CancellationToken ct = default)
    {
        var body = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PutAsync($"/api/peers/{peerId}", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NetbirdPeer>(json, _jsonOptions)!;
    }

    // ── Groups ──

    public async Task<IEnumerable<NetbirdGroup>> GetGroupsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/groups", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IEnumerable<NetbirdGroup>>(json, _jsonOptions) ?? [];
    }

    public async Task<NetbirdGroup?> GetGroupByIdAsync(string groupId,
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/groups/{groupId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NetbirdGroup>(json, _jsonOptions);
    }

    public async Task<NetbirdGroup> CreateGroupAsync(string name,
        List<string>? peerIds = null, CancellationToken ct = default)
    {
        var payload = new { name, peers = peerIds ?? [] };
        var body = new StringContent(
            JsonSerializer.Serialize(payload, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync("/api/groups", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NetbirdGroup>(json, _jsonOptions)!;
    }

    public async Task<NetbirdGroup> UpdateGroupAsync(string groupId, string name,
        List<string> peerIds, CancellationToken ct = default)
    {
        var payload = new { name, peers = peerIds };
        var body = new StringContent(
            JsonSerializer.Serialize(payload, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PutAsync($"/api/groups/{groupId}", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NetbirdGroup>(json, _jsonOptions)!;
    }

    public async Task DeleteGroupAsync(string groupId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/groups/{groupId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Setup Keys ──

    public async Task<IEnumerable<NetbirdSetupKey>> GetSetupKeysAsync(
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/setup-keys", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IEnumerable<NetbirdSetupKey>>(json, _jsonOptions) ?? [];
    }

    public async Task<NetbirdSetupKey?> GetSetupKeyByIdAsync(string keyId,
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/setup-keys/{keyId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NetbirdSetupKey>(json, _jsonOptions);
    }

    public async Task<NetbirdSetupKey> CreateSetupKeyAsync(CreateSetupKeyRequest request,
        CancellationToken ct = default)
    {
        var payload = new
        {
            name = request.Name,
            type = request.Type,
            expires_in = request.ExpiresInSeconds,
            auto_groups = request.AutoGroups,
            usage_limit = request.UsageLimit,
            ephemeral = request.Ephemeral
        };

        var body = new StringContent(
            JsonSerializer.Serialize(payload, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync("/api/setup-keys", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NetbirdSetupKey>(json, _jsonOptions)!;
    }

    public async Task DeleteSetupKeyAsync(string keyId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/setup-keys/{keyId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Policies ──

    public async Task<IEnumerable<NetbirdPolicy>> GetPoliciesAsync(
        CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/policies", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().Select(policy => ReadPolicySummary(policy)).ToList()
            : [];
    }

    public async Task<NetbirdPolicy> CreatePolicyAsync(CreatePolicyRequest request,
        CancellationToken ct = default)
    {
        var payload = new
        {
            name = request.Name,
            description = request.Description,
            enabled = request.Enabled,
            rules = request.Rules.Select(r => new
            {
                name = r.Name,
                action = r.Action,
                bidirectional = r.Bidirectional,
                protocol = r.Protocol,
                sources = r.Sources,
                destinations = r.Destinations,
                ports = r.Ports ?? []
            })
        };

        var body = new StringContent(
            JsonSerializer.Serialize(payload, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync("/api/policies", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        // NetBird returns policy rule sources/destinations as rich objects even
        // when the create request accepts group ids. Only the policy identity is
        // needed by callers, so avoid deserializing the provider-specific rule
        // shape into our simpler internal model.
        using var document = JsonDocument.Parse(json);
        return ReadPolicySummary(document.RootElement, request);
    }

    private static NetbirdPolicy ReadPolicySummary(JsonElement root, CreatePolicyRequest? fallback = null) =>
        new()
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            Name = root.TryGetProperty("name", out var name)
                ? name.GetString() ?? fallback?.Name ?? string.Empty
                : fallback?.Name ?? string.Empty,
            Description = root.TryGetProperty("description", out var description)
                ? description.GetString() ?? fallback?.Description ?? string.Empty
                : fallback?.Description ?? string.Empty,
            Enabled = root.TryGetProperty("enabled", out var enabled)
                ? enabled.GetBoolean()
                : fallback?.Enabled ?? false,
            Rules = []
        };

    public async Task DeletePolicyAsync(string policyId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/policies/{policyId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Routes ──

    public async Task<IEnumerable<NetbirdRoute>> GetRoutesAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/routes", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IEnumerable<NetbirdRoute>>(json, _jsonOptions) ?? [];
    }

    // ── Peers (accessible) ──

    public async Task<IEnumerable<NetbirdPeer>> GetAccessiblePeersAsync(
        string peerId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/peers/{peerId}/accessible-peers", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IEnumerable<NetbirdPeer>>(json, _jsonOptions) ?? [];
    }

    // ── Events ──

    public async Task<IEnumerable<NetbirdEvent>> GetEventsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/events", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IEnumerable<NetbirdEvent>>(json, _jsonOptions) ?? [];
    }
}
