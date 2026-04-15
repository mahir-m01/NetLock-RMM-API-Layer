// ─────────────────────────────────────────────────────────────────────────────
// TeamsNotification.cs
// Pattern: Strategy (concrete) — implements INotificationChannel using a
// Microsoft Teams incoming webhook.
//
// Teams webhooks accept a simple JSON payload: { title, text }
// The webhook URL is configured in appsettings (Notifications:Teams:WebhookUrl).
//
// The HttpClient is provided by NotificationFactory which calls
// _httpFactory.CreateClient("teams") — this uses the named client registered
// in Program.cs via builder.Services.AddHttpClient("teams").
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application.Notifications;

using ControlIT.Api.Domain.Interfaces;
using System.Text.Json;

public class TeamsNotification : INotificationChannel
{
    private readonly HttpClient _http;
    private readonly string _webhookUrl;

    public TeamsNotification(HttpClient http, string webhookUrl)
    {
        _http = http;
        _webhookUrl = webhookUrl;
    }

    public async Task SendAsync(string subject, string body,
        CancellationToken cancellationToken = default)
    {
        // Teams webhook format: { "title": "...", "text": "..." }
        var payload = new { title = subject, text = body };

        // StringContent wraps the serialized JSON and sets the Content-Type header.
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        // POST to the webhook URL. Teams returns 1 (plain text) on success.
        var response = await _http.PostAsync(_webhookUrl, content, cancellationToken);
        // EnsureSuccessStatusCode throws HttpRequestException on 4xx/5xx responses.
        response.EnsureSuccessStatusCode();
    }
}
