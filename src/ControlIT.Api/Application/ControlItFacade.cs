// ─────────────────────────────────────────────────────────────────────────────
// ControlItFacade.cs
// Pattern: Facade — provides a single, simplified entry point for all business
// operations. Endpoints call through the facade; they do not talk to repositories
// or SignalR directly.
//
// WHY Facade: Endpoints are responsible for HTTP concerns (parsing request,
// returning correct status codes). The facade is responsible for business logic
// (which repositories to call, in what order, with what data). Keeping them
// separate means the business logic can be tested without an HTTP stack.
//
// CRITICAL: Must be registered SCOPED (not Singleton). It holds Scoped dependencies
// (IDeviceRepository, IEventRepository, etc. and TenantContext). If it were
// Singleton, it would capture the Scoped instances from the first request and
// reuse them for all subsequent requests — a "captive dependency" bug that causes
// tenant data leakage between requests.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Application;

using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

public class ControlItFacade
{
    private readonly IDeviceRepository _devices;
    private readonly IEventRepository _events;
    private readonly ITenantRepository _tenants;
    private readonly ICommandDispatcher _commands;
    private readonly IEndpointProvider _endpoint;
    private readonly IAuditService _audit;
    private readonly ILogger<ControlItFacade> _logger;

    public ControlItFacade(
        IDeviceRepository devices,
        IEventRepository events,
        ITenantRepository tenants,
        ICommandDispatcher commands,
        IEndpointProvider endpoint,
        IAuditService audit,
        ILogger<ControlItFacade> logger)
    {
        _devices = devices;
        _events = events;
        _tenants = tenants;
        _commands = commands;
        _endpoint = endpoint;
        _audit = audit;
        _logger = logger;
    }

    // ── Devices ───────────────────────────────────────────────────────────────

    // Returns paginated devices mapped to DeviceResponse DTOs.
    // WHY map to DTO here (not in the endpoint): The facade decides what data
    // the caller gets. The endpoint just shapes the HTTP response.
    public async Task<PagedResult<DeviceResponse>> GetDevicesAsync(
        DeviceFilter filter, TenantContext tenant,
        CancellationToken ct = default)
    {
        var (items, total) = await _devices.GetAllAsync(filter, tenant, ct);

        // Compute online threshold once to avoid calling UtcNow per-device in the projection.
        var onlineThreshold = DateTime.UtcNow.AddMinutes(-5);

        var dtos = items.Select(d => new DeviceResponse
        {
            Id = d.Id,
            DeviceName = d.DeviceName,
            Platform = d.Platform,
            OperatingSystem = d.OperatingSystem,
            IpAddressInternal = d.IpAddressInternal,
            CpuUsage = d.CpuUsage,
            RamUsage = d.RamUsage,
            // IsOnline: true if the device checked in within the last 5 minutes.
            // This is computed in the application layer, not in SQL, so the definition
            // of "online" can be changed without a DB migration.
            IsOnline = d.LastAccess >= onlineThreshold,
            LastAccess = d.LastAccess
        });

        return new PagedResult<DeviceResponse>
        {
            Items = dtos,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<DeviceResponse?> GetDeviceByIdAsync(
        int id, TenantContext tenant,
        CancellationToken ct = default)
    {
        var d = await _devices.GetByIdAsync(id, tenant, ct);
        // Null return signals "not found" — the endpoint maps this to 404.
        if (d is null) return null;

        var onlineThreshold = DateTime.UtcNow.AddMinutes(-5);
        return new DeviceResponse
        {
            Id = d.Id,
            DeviceName = d.DeviceName,
            Platform = d.Platform,
            OperatingSystem = d.OperatingSystem,
            IpAddressInternal = d.IpAddressInternal,
            CpuUsage = d.CpuUsage,
            RamUsage = d.RamUsage,
            IsOnline = d.LastAccess >= onlineThreshold,
            LastAccess = d.LastAccess
        };
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<DashboardSummary> GetDashboardSummaryAsync(
        TenantContext tenant, CancellationToken ct = default)
    {
        // PageSize=1 retrieves TotalCount via SQL_CALC_FOUND_ROWS without fetching all rows.
        var (_, totalDevices) = await _devices.GetAllAsync(
            new DeviceFilter { PageSize = 1 }, tenant, ct);

        // GetOnlineCountAsync issues SELECT COUNT(*) WHERE last_access >= DATE_SUB(NOW(), INTERVAL 5 MINUTE).
        var onlineCount = await _devices.GetOnlineCountAsync(tenant, ct);

        var (_, totalEvents) = await _events.GetAllAsync(tenant, 1, 0, ct);

        return new DashboardSummary
        {
            TotalDevices = totalDevices,
            OnlineDevices = onlineCount,
            TotalTenants = 1,                 // Phase 1: single-tenant architecture
            TotalEvents = totalEvents,
            CriticalAlerts = 0               // Phase 2: Wazuh integration
        };
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    // Executes a shell command on a managed endpoint via SignalR.
    // This method handles the business logic; CommandEndpoints handles audit logging.
    public async Task<CommandResult> ExecuteCommandAsync(
        CommandRequest request, TenantContext tenant,
        CancellationToken ct = default)
    {
        // Pre-flight: fail fast if the SignalR hub is not connected.
        if (!_endpoint.IsConnected)
            throw new InvalidOperationException(
                "NetLock hub is not connected. Command dispatch unavailable.");

        // GetAccessKeyAsync returns null if the device does not exist in this tenant's scope.
        var accessKey = await _devices.GetAccessKeyAsync(request.DeviceId, tenant, ct)
            ?? throw new KeyNotFoundException(
                $"Device {request.DeviceId} not found in tenant scope.");

        // Dispatch the command via the injected dispatcher (SignalRCommandDispatcher).
        // Throws TimeoutException or InvalidOperationException on failure.
        return await _commands.DispatchAsync(accessKey, request, ct);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public async Task<PagedResult<EventResponse>> GetEventsAsync(
        TenantContext tenant, int page, int pageSize,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;
        var (items, total) = await _events.GetAllAsync(tenant, pageSize, offset, ct);

        return new PagedResult<EventResponse>
        {
            Items = items.Select(e => new EventResponse
            {
                Id = e.Id,
                DeviceName = e.DeviceName,
                Severity = e.Severity,
                Event = e.Event,       // Already mapped from `_event` column in MySqlEventRepository
                Description = e.Description,
                Timestamp = e.Timestamp  // Already mapped from `date` column
            }),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
