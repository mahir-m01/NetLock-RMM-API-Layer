namespace ControlIT.Api.Infrastructure.Persistence;

using ControlIT.Api.Common.Configuration;
using ControlIT.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;
using MySqlConnector;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration config, IOptions<DatabaseOptions> dbOptions)
    {
        var raw = config.GetConnectionString("ControlIt")
            ?? throw new InvalidOperationException(
                "Connection string 'ControlIt' is required.");

        var opts = dbOptions.Value;
        var csb = new MySqlConnectionStringBuilder(raw)
        {
            MaximumPoolSize = (uint)opts.MaxPoolSize,
            MinimumPoolSize = (uint)opts.MinPoolSize,
            ConnectionLifeTime = (uint)opts.ConnectionLifetimeSeconds,
            ConnectionTimeout = (uint)opts.ConnectionTimeoutSeconds
        };

        _connectionString = csb.ConnectionString;
    }

    public async Task<MySqlConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}
