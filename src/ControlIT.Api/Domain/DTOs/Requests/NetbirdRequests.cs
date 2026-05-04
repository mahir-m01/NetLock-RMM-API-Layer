namespace ControlIT.Api.Domain.DTOs.Requests;

public record CreateSetupKeyRequest(
    string Name,
    string Type,
    int ExpiresInSeconds,
    List<string> AutoGroups,
    int UsageLimit = 0,
    bool Ephemeral = false);

public record CreatePolicyRequest(
    string Name,
    string Description,
    bool Enabled,
    List<PolicyRuleRequest> Rules);

public record PolicyRuleRequest(
    string Name,
    string Action,
    bool Bidirectional,
    string Protocol,
    List<string> Sources,
    List<string> Destinations,
    List<string>? Ports = null);

public record UpdatePeerRequest(
    string Name,
    bool SshEnabled,
    bool LoginExpirationEnabled,
    bool InactivityExpirationEnabled);

// ── API-layer request DTOs (Phase 2D/2E) ────────────────────────────────────
// These are the shapes accepted by NetworkEndpoints, not the shapes sent to
// the Netbird API. The endpoint translates ExpiresInDays to ExpiresInSeconds
// before calling INetbirdClient.

public record CreateSetupKeyApiRequest(
    string Name,
    string Type,
    int ExpiresInDays,
    int UsageLimit = 0,
    bool Ephemeral = false);

public record EnrolPeerRequest(string SetupKey, int? DeviceId = null);

public record LinkPeerRequest(int DeviceId);

public record BindTenantGroupRequest(
    string GroupId,
    string Mode = "external");
