// WazuhOptions.cs — Config for the Wazuh SIEM integration (Phase 2).
// Wazuh is a security platform that collects and analyzes security events from managed agents.
// Phase 1: this class exists so the Options pattern is wired, but Wazuh endpoints
// are gated behind Wazuh:Enabled = true and only registered if the flag is set.

namespace ControlIT.Api.Common.Configuration;

/// <summary>
/// Binds to the "Wazuh" section in appsettings.json.
/// Phase 2 feature — set Enabled = false until a Wazuh instance is available.
/// </summary>
public class WazuhOptions
{
    /// <summary>
    /// Gate flag. When false, Wazuh endpoints are NOT registered and IWazuhClient is not injected.
    /// This prevents startup failures when Wazuh is not yet deployed.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The base URL of the Wazuh API server.
    /// Example: "https://wazuh.yourdomain.com:55000"
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// JWT token obtained from Wazuh's /security/user/authenticate endpoint.
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
