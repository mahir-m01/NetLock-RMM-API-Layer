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

// IDbConnectionFactory is the interface (contract). MySqlConnectionFactory is the
// concrete class that actually creates MySQL connections.
// In DI: builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>()
// → any class that asks for IDbConnectionFactory receives this instance.
public class MySqlConnectionFactory : IDbConnectionFactory
{
    // The connection string is read once at startup and stored in a private field.
    // `readonly` means it can only be set in the constructor — prevents accidental mutation.
    private readonly string _connectionString;

    // IConfiguration is the built-in ASP.NET config system (reads appsettings.json,
    // environment variables, etc.). It's injected automatically by the DI container.
    public MySqlConnectionFactory(IConfiguration config)
    {
        // GetConnectionString("ControlIt") reads the "ConnectionStrings:ControlIt" key.
        // The ?? throw pattern throws immediately at startup if the config is missing —
        // better than failing silently on the first DB request.
        _connectionString = config.GetConnectionString("ControlIt")
            ?? throw new InvalidOperationException(
                "Connection string 'ControlIt' is required.");
    }

    public async Task<MySqlConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        // Create a new connection object and open it asynchronously.
        // Each call returns a fresh, open connection — callers must dispose it.
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
