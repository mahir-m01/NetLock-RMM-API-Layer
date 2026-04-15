// ─────────────────────────────────────────────────────────────────────────────
// SmtpNotification.cs
// Pattern: Strategy (concrete) — implements INotificationChannel using SMTP.
//
// This class is created by NotificationFactory.Create("smtp") — it is NOT
// registered in the DI container directly. The factory creates instances
// with the correct configuration values from appsettings.
//
// WHY System.Net.Mail (not a third-party SMTP library): Zero additional
// dependencies. For Phase 1 alerting, the built-in SmtpClient is sufficient.
// Phase 2 can replace with MailKit if TLS or OAuth is needed.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application.Notifications;

using ControlIT.Api.Domain.Interfaces;
using System.Net.Mail;

public class SmtpNotification : INotificationChannel
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _from;
    private readonly string _to;

    // Constructor takes individual parameters (not IConfiguration) because
    // NotificationFactory extracts the values from config and passes them here.
    // This keeps SmtpNotification testable — you can instantiate it directly in tests.
    public SmtpNotification(string host, int port, string from, string to)
    {
        _host = host;
        _port = port;
        _from = from;
        _to = to;
    }

    public async Task SendAsync(string subject, string body,
        CancellationToken cancellationToken = default)
    {
        // SmtpClient is IDisposable — using ensures the underlying TCP connection is closed.
        // MailMessage is also IDisposable (due to Attachments collection).
        using var client = new SmtpClient(_host, _port);
        using var message = new MailMessage(_from, _to, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}
