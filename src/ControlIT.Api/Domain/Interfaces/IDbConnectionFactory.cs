// ─────────────────────────────────────────────────────────────────────────────
// IDbConnectionFactory.cs
// Pattern: Factory — creates database connections on demand without callers
// needing to know the connection string or the specific MySQL driver.
//
// WHY: Singletons can't hold open connections (not thread-safe for long-lived
// MySQL connections), so each request asks the factory for a fresh connection.
// The factory itself is Singleton (it only holds the connection string).
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.Interfaces;

// In C#, interfaces define a "contract" — similar to TypeScript interfaces.
// They're also the foundation of Dependency Injection: register the interface,
// inject the interface, swap the concrete class without changing callers.
public interface IDbConnectionFactory
{
    // Returns an already-opened MySqlConnection. Caller is responsible for
    // disposing it (use `using var conn = await _factory.CreateConnectionAsync()`).
    // The CancellationToken allows the caller to abort if the request is cancelled.
    Task<MySqlConnector.MySqlConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default);
}
