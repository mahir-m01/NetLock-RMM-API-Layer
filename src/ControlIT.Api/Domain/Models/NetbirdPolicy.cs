namespace ControlIT.Api.Domain.Models;

public class NetbirdPolicy
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public List<NetbirdPolicyRule> Rules { get; set; } = [];
}

public class NetbirdPolicyRule
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Action { get; set; } = string.Empty;
    public bool Bidirectional { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = [];
    public List<string> Destinations { get; set; } = [];
    public List<string> Ports { get; set; } = [];
}
