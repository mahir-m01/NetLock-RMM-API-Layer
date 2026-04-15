// DatabaseOptions.cs — Config for the MySQL database name.
// Needed by NetLockSchemaValidator to query information_schema.COLUMNS
// and verify that all required columns exist in NetLock's schema.

namespace ControlIT.Api.Common.Configuration;

/// <summary>
/// Binds to the "Database" section in appsettings.json.
/// Currently only stores the database name used for schema validation.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// The MySQL database (schema) name where both NetLock and ControlIT tables live.
    /// Used by NetLockSchemaValidator in its information_schema query.
    /// Must match the Database= part of the ConnectionStrings:ControlIt connection string.
    /// </summary>
    public string Name { get; set; } = "netlock";
}
