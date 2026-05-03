// ─────────────────────────────────────────────────────────────────────────────
// DashboardEndpoints.cs
// Registers:
//   GET /dashboard    — full DashboardSummary (JSON, one-shot)
//   GET /sync/stream  — Server-Sent Events stream for the sync indicator
//
// WHY SSE instead of polling:
// Polling forces every client to wake up independently on a timer and issue a
// request. With SSE the server pushes one event every 30 s on an already-open
// HTTP/1.1 connection. Clients receive updates instantly when pushed; the server
// controls the cadence. One server-side DB query per interval serves all
// connected clients — far cheaper than N independent poll requests.
//
// CRITICAL: OnlineDevices MUST come from a real COUNT query — never hardcode -1
// or any placeholder value. The dashboard uses this to show device health.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Endpoints;

using System.Text.Json;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.DTOs.Responses;

public static class DashboardEndpoints
{
    private static readonly JsonSerializerOptions SseJson = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        // GET /dashboard
        // Returns the full DashboardSummary for the authenticated tenant.
        app.MapGet("/dashboard", async (
            ControlItFacade facade,
            TenantContext tenant) =>
        {
            var summary = await facade.GetDashboardSummaryAsync(tenant);
            return Results.Ok(summary);
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");

        MapDashboardStream(app, "/dashboard/stream");
        MapDashboardStream(app, "/sync/stream");
    }

    private static void MapDashboardStream(WebApplication app, string path)
    {
        app.MapGet(path, async (
            ControlItFacade facade,
            IPushEventPublisher events,
            TenantContext tenant,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";  // disable nginx buffering

            try
            {
                var scope = PushSubscriptionScope.From(tenant);
                var snapshot = await facade.GetDashboardPushSnapshotAsync(tenant, ct);
                foreach (var evt in snapshot)
                    await WriteSseAsync(ctx, evt, ct);

                await foreach (var evt in events.SubscribeAsync(scope, ct))
                    await WriteSseAsync(ctx, evt, ct);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — normal exit, no error to propagate.
            }
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
    }

    private static async Task WriteSseAsync(
        HttpContext ctx,
        PushEventEnvelope evt,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(evt, SseJson);
        await ctx.Response.WriteAsync($"event: {evt.Type}\n", ct);
        await ctx.Response.WriteAsync($"data: {payload}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
}
