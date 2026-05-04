namespace ControlIT.Api.Domain.Models;

using System.Text.Json.Serialization;

public class NetbirdEvent
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Activity { get; set; } = string.Empty;
    public string ActivityCode { get; set; } = string.Empty;
    public string InitiatorId { get; set; } = string.Empty;
    public string InitiatorName { get; set; } = string.Empty;
    public string InitiatorEmail { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, object>? Meta { get; set; }
}
