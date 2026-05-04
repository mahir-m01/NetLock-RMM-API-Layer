// ─────────────────────────────────────────────────────────────────────────────
// MySqlContainerFixture.cs
// Shared xUnit fixture that spins up a disposable MySQL container via
// Testcontainers. All integration tests in the "Database" collection share a
// single container instance, keeping startup cost amortised across the suite.
// ─────────────────────────────────────────────────────────────────────────────
using MySqlConnector;
using Testcontainers.MySql;
using Xunit;

namespace ControlIT.Api.Tests.Fixtures;

/// <summary>
/// Manages a MySQL 8.0 Docker container lifecycle for integration tests.
/// The container starts once before any test in the collection runs and is
/// disposed after all tests complete.
/// </summary>
public class MySqlContainerFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.0")
        .WithDatabase("netlock")
        .WithUsername("test_user")
        .WithPassword("Test@2026!")
        .Build();

    /// <summary>
    /// Full connection string pointing at the ephemeral MySQL container.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await SeedNetLockSchemaAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task SeedNetLockSchemaAsync()
    {
        // Use MySqlConnector directly rather than ExecScriptAsync, which swallows
        // non-zero exit codes and gives no signal when the mysql CLI fails.
        // Statements are executed individually so there are no multi-statement
        // delimiter issues and any failure throws immediately.
        await using var conn = new MySqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        string[] statements =
        [
            """
            CREATE TABLE IF NOT EXISTS devices (
              id INT NOT NULL,
              tenant_id INT,
              location_id INT,
              device_name VARCHAR(255),
              access_key VARCHAR(255),
              platform VARCHAR(255),
              operating_system VARCHAR(255),
              agent_version VARCHAR(255),
              cpu VARCHAR(255),
              cpu_usage FLOAT,
              ram VARCHAR(255),
              ram_usage FLOAT,
              ip_address_internal VARCHAR(255),
              ip_address_external VARCHAR(255),
              last_access DATETIME,
              authorized TINYINT(1),
              synced TINYINT(1),
              PRIMARY KEY (id)
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS tenants (
              id INT NOT NULL,
              guid VARCHAR(255),
              name VARCHAR(255),
              PRIMARY KEY (id)
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS locations (
              id INT NOT NULL,
              tenant_id INT,
              guid VARCHAR(255),
              name VARCHAR(255),
              PRIMARY KEY (id)
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS events (
              id INT NOT NULL,
              device_id INT,
              tenant_name_snapshot VARCHAR(255),
              device_name VARCHAR(255),
              date DATETIME,
              severity VARCHAR(255),
              reported_by VARCHAR(255),
              _event VARCHAR(255),
              description TEXT,
              PRIMARY KEY (id)
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS accounts (
              id INT NOT NULL,
              remote_session_token VARCHAR(255),
              PRIMARY KEY (id)
            )
            """,
            // Seed one tenant so GetByIdAsync(1) returns a result (used by elevated-valid-tenant tests)
            "INSERT IGNORE INTO tenants (id, guid, name) VALUES (1, 'test-guid-1', 'Test Tenant')",
            "INSERT IGNORE INTO locations (id, tenant_id, guid, name) VALUES (1, 1, 'test-location-guid-1', 'Test Location')",
            """
            INSERT IGNORE INTO devices (
              id, tenant_id, location_id, device_name, access_key,
              platform, operating_system, agent_version,
              cpu, cpu_usage, ram, ram_usage,
              ip_address_internal, ip_address_external,
              last_access, authorized, synced
            )
            VALUES (
              27, 1, 1, 'integration-device-27', 'integration-access-key-27',
              'Linux', 'Ubuntu 24.04 LTS', 'test-agent-1.0.0',
              'Test CPU', 0, '8 GB', 0,
              '10.0.0.27', '203.0.113.27',
              NOW(), 1, 1
            )
            """,
        ];

        foreach (var sql in statements)
        {
            await using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// xUnit collection definition that ties test classes to the shared
/// <see cref="MySqlContainerFixture"/> instance. Decorate test classes with
/// [Collection("Database")] to opt in.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<MySqlContainerFixture> { }
