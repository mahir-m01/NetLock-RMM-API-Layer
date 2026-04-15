// ─────────────────────────────────────────────────────────────────────────────
// WebhookNotification.cs
// Pattern: Strategy (concrete) — implements INotificationChannel for a
// generic HTTP webhook endpoint.
//
// Unlike TeamsNotification (which has a fixed Teams-specific payload format),
// this sends a generic JSON body: { subject, body, timestamp }.
// Use this for custom webhook receivers, n8n, Zapier, or any HTTP endpoint.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application.Notifications;

using ControlIT.Api.Domain.Interfaces;
using System.Text.Json;

public class WebhookNotification : INotificationChannel
{
    private readonly HttpClient _http;
    private readonly string _url;

    public WebhookNotification(HttpClient http, string url)
    {
        _http = http;
        _url = url;
    }

    public async Task SendAsync(string subject, string body,
        CancellationToken cancellationToken = default)
    {
        // Generic webhook payload with timestamp for idempotency checking by receivers.
        var payload = new { subject, body, timestamp = DateTime.UtcNow };
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _http.PostAsync(_url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
