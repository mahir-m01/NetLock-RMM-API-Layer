namespace ControlIT.Api.Endpoints;

using System.Diagnostics;
using System.Reflection;
using Dapper;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

public static class SystemHealthEndpoints
{
    // Process start time captured once at startup — used to compute uptime.
    private static readonly DateTime _startedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public static void Map(WebApplication app)
    {
        // GET /admin/system-health
        // SuperAdmin-only endpoint returning detailed diagnostics on every subsystem.
        // This is NOT a monitoring probe — it's an operational view for admins.
        app.MapGet("/admin/system-health", async (
            IDbConnectionFactory dbFactory,
            IEndpointProvider endpoint,
            INetbirdClient netbird,
            INetLockAdminClient netLockAdmin,
            IWebHostEnvironment env,
            IMemoryCache cache) =>
        {
            var result = await cache.GetOrCreateAsync("admin:system_health", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);

                var mysql = await CheckMysqlAsync(dbFactory);
                var signalR = await CheckSignalRAsync(endpoint, netLockAdmin);
                var netBird = await CheckNetBirdAsync(netbird);

                var coreOk = mysql.Status == "healthy" && signalR.Status == "healthy";
                var overall = !coreOk ? "unhealthy"
                            : netBird.Status != "healthy" ? "degraded"
                            : "healthy";

                var version = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion
                              ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                              ?? "unknown";

                var uptime = DateTime.UtcNow - _startedAt;
                var uptimeStr = uptime.TotalHours >= 1
                    ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
                    : $"{uptime.Minutes}m {uptime.Seconds}s";

                return new SystemHealthResponse
                {
                    Status = overall,
                    CheckedAt = DateTime.UtcNow,
                    Mysql = mysql,
                    SignalR = signalR,
                    NetBird = netBird,
                    Api = new ApiInfo
                    {
                        Version = version,
                        Environment = env.EnvironmentName,
                        Uptime = uptimeStr,
                        ConnectedDevices = signalR.Detail?.StartsWith("connected=") == true
                            ? int.TryParse(signalR.Detail.Split('=')[1], out var n) ? n : 0
                            : 0,
                    },
                };
            });

            return Results.Ok(result);
        }).RequireRateLimiting("api").RequireAuthorization("SuperAdminOnly");
    }

    private static async Task<ComponentHealth> CheckMysqlAsync(IDbConnectionFactory factory)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = await factory.CreateConnectionAsync();
            var version = await conn.ExecuteScalarAsync<string>("SELECT VERSION()");
            sw.Stop();
            return new ComponentHealth
            {
                Status = "healthy",
                LatencyMs = (int)sw.ElapsedMilliseconds,
                Detail = version,
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth { Status = "unhealthy", Detail = ex.Message };
        }
    }

    private static async Task<ComponentHealth> CheckSignalRAsync(
        IEndpointProvider endpoint, INetLockAdminClient admin)
    {
        if (!endpoint.IsConnected)
            return new ComponentHealth { Status = "unhealthy", Detail = "Hub not connected" };

        try
        {
            var sw = Stopwatch.StartNew();
            var keys = await admin.GetConnectedAccessKeysAsync();
            sw.Stop();
            return new ComponentHealth
            {
                Status = "healthy",
                LatencyMs = (int)sw.ElapsedMilliseconds,
                Detail = $"connected={keys.Count()}",
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth { Status = "degraded", Detail = ex.Message };
        }
    }

    private static async Task<ComponentHealth> CheckNetBirdAsync(INetbirdClient netbird)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var peers = await netbird.GetPeersAsync();
            sw.Stop();
            return new ComponentHealth
            {
                Status = "healthy",
                LatencyMs = (int)sw.ElapsedMilliseconds,
                Detail = $"peers={peers.Count()}",
            };
        }
        catch (Exception ex)
        {
            return new ComponentHealth { Status = "unhealthy", Detail = ex.Message };
        }
    }
}
