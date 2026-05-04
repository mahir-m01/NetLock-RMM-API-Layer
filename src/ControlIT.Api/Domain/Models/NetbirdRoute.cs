namespace ControlIT.Api.Domain.Models;

public class NetbirdRoute
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string NetworkId { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string NetworkType { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Peer { get; set; } = string.Empty;
    public int Metric { get; set; }
    public bool Masquerade { get; set; }
    public List<string> Groups { get; set; } = [];
}
