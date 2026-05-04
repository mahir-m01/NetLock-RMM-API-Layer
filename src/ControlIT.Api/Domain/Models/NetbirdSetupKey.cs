namespace ControlIT.Api.Domain.Models;

public class NetbirdSetupKey
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Valid { get; set; }
    public bool Revoked { get; set; }
    public int UsedTimes { get; set; }
    public int UsageLimit { get; set; }
    public DateTime Expires { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> AutoGroups { get; set; } = [];
    public bool Ephemeral { get; set; }
    public string State { get; set; } = string.Empty;
}
