namespace ControlIT.Api.Domain.DTOs.Responses;

using System.Text.Json.Serialization;
using ControlIT.Api.Domain.Models;

public static class PushEventTypes
{
    public const string DeviceOnline = "device.online";
    public const string DeviceOffline = "device.offline";
    public const string DeviceUpdated = "device.updated";
    public const string CommandStatus = "command.status";
    public const string NetbirdPeerUpdated = "netbird.peer.updated";
    public const string SystemHealthUpdated = "system.health.updated";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        DeviceOnline,
        DeviceOffline,
        DeviceUpdated,
        CommandStatus,
        NetbirdPeerUpdated,
        SystemHealthUpdated
    };
}

public sealed record PushEventEnvelope(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tenantId")] int? TenantId,
    [property: JsonPropertyName("emittedAt")] DateTimeOffset EmittedAt,
    [property: JsonPropertyName("payload")] object Payload)
{
    public const int CurrentVersion = 1;

    public static PushEventEnvelope Create(string type, int? tenantId, object payload) =>
        new(CurrentVersion, type, tenantId, DateTimeOffset.UtcNow, payload);
}

public sealed record DevicePushPayload(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("tenantId")] int TenantId,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("operatingSystem")] string OperatingSystem,
    [property: JsonPropertyName("agentVersion")] string AgentVersion,
    [property: JsonPropertyName("ipAddressInternal")] string IpAddressInternal,
    [property: JsonPropertyName("ipAddressExternal")] string IpAddressExternal,
    [property: JsonPropertyName("isOnline")] bool IsOnline,
    [property: JsonPropertyName("cpuUsage")] double? CpuUsage,
    [property: JsonPropertyName("ramUsage")] double? RamUsage,
    [property: JsonPropertyName("lastAccess")] DateTime LastAccess,
    [property: JsonPropertyName("dashboard")] DashboardStatsPushPayload? Dashboard = null);

public sealed record DashboardStatsPushPayload(
    [property: JsonPropertyName("totalDevices")] int TotalDevices,
    [property: JsonPropertyName("onlineDevices")] int OnlineDevices,
    [property: JsonPropertyName("totalTenants")] int TotalTenants,
    [property: JsonPropertyName("totalEvents")] int TotalEvents,
    [property: JsonPropertyName("criticalAlerts")] int CriticalAlerts);

public sealed record CommandStatusPushPayload(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string? Message);

public sealed record NetbirdPeerPushPayload(
    [property: JsonPropertyName("peerId")] string PeerId,
    [property: JsonPropertyName("tenantId")] int TenantId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ip")] string Ip,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("lastSeen")] DateTime LastSeen);

public sealed record SystemHealthPushPayload(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("detail")] string? Detail,
    [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt,
    [property: JsonPropertyName("dashboard")] DashboardStatsPushPayload? Dashboard = null);

public static class PushEventFactory
{
    public static PushEventEnvelope Device(
        string type,
        Device device,
        bool isOnline,
        DashboardStatsPushPayload? dashboard = null) =>
        PushEventEnvelope.Create(type, device.TenantId, new DevicePushPayload(
            device.Id,
            device.TenantId,
            device.DeviceName,
            device.Platform,
            device.OperatingSystem,
            device.AgentVersion,
            device.IpAddressInternal,
            device.IpAddressExternal,
            isOnline,
            isOnline ? device.CpuUsage : null,
            isOnline ? device.RamUsage : null,
            device.LastAccess,
            dashboard));

    public static PushEventEnvelope DeviceUpdated(
        DeviceResponse device,
        DashboardStatsPushPayload? dashboard = null) =>
        PushEventEnvelope.Create(PushEventTypes.DeviceUpdated, device.TenantId, new DevicePushPayload(
            device.Id,
            device.TenantId,
            device.DeviceName,
            device.Platform,
            device.OperatingSystem,
            device.AgentVersion,
            device.IpAddressInternal,
            device.IpAddressExternal,
            device.IsOnline,
            device.CpuUsage,
            device.RamUsage,
            device.LastAccess,
            dashboard));

    public static PushEventEnvelope CommandStatus(int tenantId, int deviceId, string status, string? message = null) =>
        PushEventEnvelope.Create(PushEventTypes.CommandStatus, tenantId,
            new CommandStatusPushPayload(deviceId, status, message));

    public static PushEventEnvelope NetbirdPeerUpdated(int tenantId, NetbirdPeer peer) =>
        PushEventEnvelope.Create(PushEventTypes.NetbirdPeerUpdated, tenantId,
            new NetbirdPeerPushPayload(peer.Id, tenantId, peer.Name, peer.Ip, peer.Connected, peer.LastSeen));

    public static PushEventEnvelope SystemHealth(
        string component,
        string status,
        string? reason,
        DashboardStatsPushPayload? dashboard = null) =>
        PushEventEnvelope.Create(PushEventTypes.SystemHealthUpdated, null,
            new SystemHealthPushPayload(component, status, reason, reason, DateTimeOffset.UtcNow, dashboard));

    public static DashboardStatsPushPayload Dashboard(DashboardSummary summary) =>
        new(
            summary.TotalDevices,
            summary.OnlineDevices,
            summary.TotalTenants,
            summary.TotalEvents,
            summary.CriticalAlerts);
}
