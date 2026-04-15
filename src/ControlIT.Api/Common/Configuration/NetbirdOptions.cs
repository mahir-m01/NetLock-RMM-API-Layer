// NetbirdOptions.cs — Strongly-typed config for the Netbird mesh VPN integration.
// Netbird manages the peer-to-peer network overlay between managed devices.
// ControlIT reads peers and can enrol/remove devices via the Netbird Management API.

namespace ControlIT.Api.Common.Configuration;

/// <summary>
/// Binds to the "Netbird" section in appsettings.json.
/// Used by NetbirdApiClient to set base URL and auth token.
/// </summary>
public class NetbirdOptions
{
    /// <summary>
    /// The base URL of the Netbird management server.
    /// Example: "https://api.netbird.io" (cloud) or "https://your-self-hosted-netbird.com"
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// A Personal Access Token (PAT) from the Netbird dashboard.
    /// Used as "Authorization: Token &lt;TOKEN&gt;" — NOT "Bearer".
    /// Getting this wrong causes 401 responses with no helpful error message.
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
