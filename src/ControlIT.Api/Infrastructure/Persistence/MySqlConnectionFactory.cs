// ─────────────────────────────────────────────────────────────────────────────
// MySqlConnectionFactory.cs
// Pattern: Factory (concrete implementation of IDbConnectionFactory)
//
// WHY Singleton: The factory itself only holds a connection string (a string —
// completely thread-safe). Creating a new MySqlConnection per request is correct
// because MySQL connections are NOT thread-safe for concurrent use.
// The factory is long-lived; the connections it creates are short-lived.
//
// WHY open the connection here: Callers use `using var conn = await ...`
// which automatically closes the connection when the using block exits.
// Opening here keeps repository code clean — no manual Open() calls needed.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Persistence;

using ControlIT.Api.Domain.Interfaces;
using MySqlConnector;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    // Throws at construction if the connection string is missing — fail-fast at startup
    // rather than on the first DB request.
    public MySqlConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("ControlIt")
            ?? throw new InvalidOperationException(
                "Connection string 'ControlIt' is required.");
    }

    public async Task<MySqlConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        // Returns a fresh, open connection. Callers are responsible for disposal.
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
