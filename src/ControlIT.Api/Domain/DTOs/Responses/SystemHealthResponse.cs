namespace ControlIT.Api.Domain.DTOs.Responses;

public class SystemHealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public ComponentHealth Mysql { get; set; } = new();
    public ComponentHealth SignalR { get; set; } = new();
    public ComponentHealth NetBird { get; set; } = new();
    public ApiInfo Api { get; set; } = new();
}

public class ComponentHealth
{
    public string Status { get; set; } = string.Empty;
    public int? LatencyMs { get; set; }
    public string? Detail { get; set; }
}

public class ApiInfo
{
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
    public int ConnectedDevices { get; set; }
}
