// ─────────────────────────────────────────────────────────────────────────────
// NetLockSchemaValidator.cs
// Pattern: Fail-Fast / Guard — converts schema drift from a silent runtime
// failure into a visible startup crash.
//
// WHY this is critical: Dapper with MatchNamesWithUnderscores maps MySQL columns
// to C# properties by name. If NetLock renames a column (e.g., last_access →
// last_seen), Dapper silently maps it to DateTime.MinValue (the default).
// No exception is thrown. The API returns wrong data.
//
// This validator queries information_schema.COLUMNS at startup to verify every
// column that any Dapper query in ControlIT reads. If any is missing, it throws
// InvalidOperationException with a list of missing columns — the app won't start.
//
// MAINTENANCE CONTRACT: Every time a new Dapper query is added, add the new
// (Table, Column) pairs to RequiredColumns. This is enforced by code review.
//
// Registered as Singleton — IDbConnectionFactory and IConfiguration are both
// Singletons, so no captive dependency problem.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.NetLock;

using Dapper;
using ControlIT.Api.Domain.Interfaces;

public class NetLockSchemaValidator : ISchemaValidator
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<NetLockSchemaValidator> _logger;
    private readonly string _databaseName;

    /// <summary>
    /// Every (Table, Column) pair that any Dapper query in ControlIT reads.
    /// MAINTENANCE CONTRACT: Add entries here whenever a new Dapper query is added.
    /// Enforced by code review — never skip this step.
    /// </summary>
    private static readonly (string Table, string Column)[] RequiredColumns =
    [
        ("devices",   "id"),
        ("devices",   "tenant_id"),
        ("devices",   "location_id"),
        ("devices",   "device_name"),
        ("devices",   "access_key"),
        ("devices",   "platform"),
        ("devices",   "operating_system"),
        ("devices",   "agent_version"),
        ("devices",   "cpu"),
        ("devices",   "cpu_usage"),
        ("devices",   "ram"),
        ("devices",   "ram_usage"),
        ("devices",   "ip_address_internal"),
        ("devices",   "ip_address_external"),
        ("devices",   "last_access"),
        ("devices",   "authorized"),
        ("devices",   "synced"),
        ("tenants",   "id"),
        ("tenants",   "guid"),
        ("tenants",   "name"),
        ("locations", "id"),
        ("locations", "tenant_id"),
        ("locations", "guid"),
        ("locations", "name"),
        ("events",    "id"),
        ("events",    "device_id"),
        ("events",    "tenant_name_snapshot"),
        ("events",    "device_name"),
        ("events",    "date"),
        ("events",    "severity"),
        ("events",    "reported_by"),
        ("events",    "_event"),
        ("events",    "description"),
        ("accounts",  "remote_session_token"),
    ];

    public NetLockSchemaValidator(
        IDbConnectionFactory factory,
        ILogger<NetLockSchemaValidator> logger,
        IConfiguration config)
    {
        _factory = factory;
        _logger = logger;
        // Scopes the information_schema.COLUMNS query to the correct database.
        _databaseName = config["Database:Name"] ?? "netlock";
    }

    public async Task ValidateRequiredColumnsAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _factory.CreateConnectionAsync(cancellationToken);

        var tables = RequiredColumns.Select(c => c.Table).Distinct().ToArray();

        // Query information_schema.COLUMNS for all columns in the relevant tables.
        // 'Table' is a reserved keyword in MySQL — the alias TableName avoids the conflict.
        // Dapper tuple mapping requires the alias names to match the tuple field names exactly.
        var existing = (await conn.QueryAsync<(string TableName, string ColumnName)>(
            @"SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName
              FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db
              AND TABLE_NAME IN @tables",
            new { db = _databaseName, tables }))
            .Select(r => (Table: r.TableName, Column: r.ColumnName))
            .ToHashSet();

        var missing = RequiredColumns
            .Where(req => !existing.Contains(req))
            .ToList();

        if (missing.Count > 0)
        {
            var detail = string.Join("\n  ",
                missing.Select(m => $"{m.Table}.{m.Column}"));

            var message =
                $"NetLock schema validation failed. Missing columns:\n  {detail}\n" +
                "A NetLock update likely renamed or removed these columns. " +
                "Update the Dapper queries and this validator's RequiredColumns list " +
                "before restarting.";

            // LogCritical so the failure appears in production monitoring.
            _logger.LogCritical("{Message}", message);

            // Throw to prevent the app from starting — intentional fail-fast behaviour.
            throw new InvalidOperationException(message);
        }

        _logger.LogInformation(
            "NetLock schema validation passed — {Count} required columns verified.",
            RequiredColumns.Length);
    }
}
