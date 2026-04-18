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

public static class DashboardEndpoints
{
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

        // GET /sync/stream
        // Server-Sent Events endpoint. Keeps the HTTP connection open and pushes
        // a sync event every 30 seconds. The frontend uses fetch() + ReadableStream
        // (not EventSource) so that it can send the x-api-key header.
        //
        // Event format:
        //   data: {"syncedAt":"...","onlineDevices":3,"totalDevices":5}\n\n
        //
        // The CancellationToken fires when the client disconnects (browser tab
        // closed, navigation, explicit abort). The while-loop exits cleanly.
        app.MapGet("/sync/stream", async (
            ControlItFacade facade,
            TenantContext tenant,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.ContentType  = "text/event-stream";
            ctx.Response.Headers["Cache-Control"]    = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";  // disable nginx buffering

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var summary = await facade.GetDashboardSummaryAsync(tenant, ct);

                    var payload = JsonSerializer.Serialize(new
                    {
                        syncedAt      = summary.ServerTime,
                        onlineDevices = summary.OnlineDevices,
                        totalDevices  = summary.TotalDevices
                    });

                    // SSE wire format: "data: <json>\n\n"
                    await ctx.Response.WriteAsync($"data: {payload}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);

                    // Wait 30 s before next push. Cancelled immediately on disconnect.
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected — normal exit, no error to propagate.
            }
        }).RequireRateLimiting("api").RequireAuthorization("TenantMember");
    }
}
