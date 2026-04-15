// ─────────────────────────────────────────────────────────────────────────────
// ISchemaValidator.cs
// Pattern: Fail-Fast / Guard — validates assumptions at startup before any
// traffic is accepted. Converts silent runtime mapping failures into a visible
// crash with a descriptive error message.
//
// WHY: Dapper silently maps missing columns to null/default with NO exception.
// If NetLock renames a column (e.g., `last_access` → `last_seen`), every
// device query would return DateTime.MinValue without any error. This validator
// checks ALL required columns exist at startup, preventing silent data corruption.
//
// Registered as Singleton — it has no Scoped dependencies (IDbConnectionFactory
// and IConfiguration are both Singletons), so there's no captive dependency risk.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

public interface ISchemaValidator
{
    /// <summary>
    /// Validates all required NetLock schema columns exist.
    /// Throws InvalidOperationException with a descriptive message if any are missing.
    /// Called at startup by NetLockSignalRService.StartAsync — the app won't start
    /// if the schema doesn't match what ControlIT's Dapper queries expect.
    /// </summary>
    Task ValidateRequiredColumnsAsync(CancellationToken cancellationToken = default);
}
