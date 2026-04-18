namespace ControlIT.Api.Domain.DTOs.Responses;

public class AuditLogResponse
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int TenantId { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? IpAddress { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
