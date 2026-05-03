namespace ControlIT.Api.Domain.Models;

public class DeviceNetbirdMap
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string NetbirdPeerId { get; set; } = string.Empty;
    public string NetbirdIp { get; set; } = string.Empty;
    public string NetbirdHostname { get; set; } = string.Empty;
    public DateTime MappedAt { get; set; }
    public string MappedBy { get; set; } = string.Empty;
}
