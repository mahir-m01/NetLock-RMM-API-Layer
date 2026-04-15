// ─────────────────────────────────────────────────────────────────────────────
// INotificationChannel.cs
// Pattern: Strategy — multiple notification channels (SMTP, Teams, Webhook)
// implement the same interface, so NotificationFactory can return any of them
// and callers don't need to know which channel is active.
//
// WHY: Adding a new notification channel (e.g., Slack, PagerDuty) only requires
// implementing this interface and adding a case to NotificationFactory.Create().
// No existing code needs to change — this is the Open/Closed Principle in action.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

public interface INotificationChannel
{
    // Sends a notification. subject and body are generic — each channel formats
    // them appropriately (email subject line, Teams card title, etc.).
    Task SendAsync(string subject, string body, CancellationToken cancellationToken = default);
}
