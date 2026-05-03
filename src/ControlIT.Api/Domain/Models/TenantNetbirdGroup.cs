namespace ControlIT.Api.Domain.Models;

public class TenantNetbirdGroup
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string NetbirdGroupId { get; set; } = string.Empty;
    public string NetbirdGroupName { get; set; } = string.Empty;
    public string IsolationPolicyId { get; set; } = string.Empty;
    public string GroupMode { get; set; } = TenantNetbirdGroupMode.Managed;
    public bool ControlItManaged { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class TenantNetbirdGroupMode
{
    public const string Managed = "managed";
    public const string External = "external";
    public const string ReadOnly = "read_only";

    public static bool IsValid(string? mode) =>
        mode is Managed or External or ReadOnly;
}
