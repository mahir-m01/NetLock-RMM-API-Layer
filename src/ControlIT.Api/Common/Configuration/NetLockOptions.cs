// NetLockOptions.cs — Strongly-typed configuration binding for the NetLock integration.
// The Options pattern binds a JSON section to a typed class, giving compile-time safety
// over raw config key lookups. Inject IOptions<NetLockOptions> to access these values.

namespace ControlIT.Api.Common.Configuration;

/// <summary>
/// Binds to the "NetLock" section in appsettings.json.
/// Injected via IOptions&lt;NetLockOptions&gt; — see Program.cs where Configure&lt;NetLockOptions&gt; is called.
/// </summary>
public class NetLockOptions
{
    /// <summary>
    /// The full URL of NetLock's SignalR command hub.
    /// Example: "http://localhost:7080/commandHub"
    /// </summary>
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>
    /// The remote_session_token from NetLock's accounts table for the admin account.
    /// Used only for the SignalR /commandHub connection. NEVER log this value.
    /// Query: SELECT remote_session_token FROM accounts WHERE is_admin = 1 LIMIT 1;
    /// </summary>
    public string AdminSessionToken { get; set; } = string.Empty;

    /// <summary>
    /// The files_api_key from NetLock's settings table.
    /// Used for admin REST endpoints (e.g. GET /admin/devices/connected).
    /// Different from AdminSessionToken — this is stable across container restarts.
    /// Query: SELECT files_api_key FROM settings LIMIT 1;
    /// </summary>
    public string FilesApiKey { get; set; } = string.Empty;
}
