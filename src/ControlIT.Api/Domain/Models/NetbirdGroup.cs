namespace ControlIT.Api.Domain.Models;

public class NetbirdGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PeersCount { get; set; }
    public int ResourcesCount { get; set; }
    public string Issued { get; set; } = string.Empty;
    public List<NetbirdPeerRef> Peers { get; set; } = [];
}

public class NetbirdPeerRef
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
