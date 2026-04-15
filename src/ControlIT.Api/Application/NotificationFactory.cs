// ─────────────────────────────────────────────────────────────────────────────
// NotificationFactory.cs
// Pattern: Factory — creates INotificationChannel instances based on a string
// channel type key ("smtp", "teams", "webhook").
//
// WHY instance class (NOT static): Static classes can't be injected. By making
// this an instance class registered Scoped in DI, callers can receive it via
// constructor injection and test it with a mock.
//
// WHY IHttpClientFactory: HttpClient instances should come from the factory
// to avoid socket exhaustion. Named clients ("teams", "webhook") are pre-registered
// in Program.cs. The factory manages connection pooling and DNS refresh.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application;

using ControlIT.Api.Application.Notifications;
using ControlIT.Api.Domain.Interfaces;

public class NotificationFactory
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public NotificationFactory(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    // Returns an INotificationChannel configured from appsettings for the given type key.
    public INotificationChannel Create(string channelType) => channelType switch
    {
        "smtp" => new SmtpNotification(
            host: _config["Notifications:Smtp:Host"] ?? "localhost",
            port: int.Parse(_config["Notifications:Smtp:Port"] ?? "25"),
            from: _config["Notifications:Smtp:From"] ?? "noreply@controlit.local",
            to: _config["Notifications:Smtp:To"] ?? string.Empty),

        "teams" => new TeamsNotification(
            http: _httpFactory.CreateClient("teams"),
            webhookUrl: _config["Notifications:Teams:WebhookUrl"] ?? string.Empty),

        "webhook" => new WebhookNotification(
            http: _httpFactory.CreateClient("webhook"),
            url: _config["Notifications:Webhook:Url"] ?? string.Empty),

        // Throw on unrecognised channel types so misconfiguration is visible immediately.
        _ => throw new ArgumentException($"Unknown notification channel type: '{channelType}'")
    };
}
