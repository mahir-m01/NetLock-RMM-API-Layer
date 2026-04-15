// ─────────────────────────────────────────────────────────────────────────────
// InfiniteRetryPolicy.cs
// Pattern: Strategy — implements IRetryPolicy for the SignalR client's
// automatic reconnection behaviour.
//
// WHY infinite (never return null): The dashboard must always be able to
// reach managed endpoints. A finite retry policy means the connection
// permanently fails after N retries — someone has to manually restart the
// service. With infinite retry, temporary network issues self-heal.
//
// WHY cap at 90 seconds: Without the cap, exponential backoff would grow to
// hours (2^20 = ~12 days). A cap of 60-90 seconds means worst-case the
// service retries every 90 seconds — fast enough to recover without flooding
// the NetLock hub with reconnect attempts.
//
// WHY ±20% jitter: If multiple ControlIT instances (or multiple reconnects)
// all retry at the exact same time (e.g., after a hub restart), they'd all
// hit the hub simultaneously — "thundering herd". Jitter spreads them out.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using Microsoft.AspNetCore.SignalR.Client;

// IRetryPolicy is the SignalR interface for custom reconnection strategies.
// Returning null from NextRetryDelay would STOP retrying — we never do that.
public class InfiniteRetryPolicy : IRetryPolicy
{
    // Maximum delay between retries. After the cap, each retry waits ~90s (+jitter).
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(90);

    // Called by the SignalR client after each failed reconnect attempt.
    // retryContext.PreviousRetryCount is how many times we've already retried.
    // retryContext.RetryReason contains the exception that caused the disconnect.
    // Return: the TimeSpan to wait before the next attempt. NEVER return null.
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 32s, 64s → capped at 90s
        var rawDelay = Math.Pow(2, retryContext.PreviousRetryCount);
        var cappedDelay = Math.Min(MaxDelay.TotalSeconds, rawDelay);
        var baseDelay = TimeSpan.FromSeconds(cappedDelay);

        // ±20% jitter: multiply by a random factor between 0.8 and 1.2
        // Random.Shared is thread-safe in .NET 6+ — no lock needed.
        var jitterFactor = Random.Shared.NextDouble() * 0.4 - 0.2;
        var jitter = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitterFactor);

        // Never return null — that would stop all retries permanently.
        return baseDelay + jitter;
    }
}
