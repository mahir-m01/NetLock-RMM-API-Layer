namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public class ControlItDbContextOwnershipTests
{
    [Fact]
    public void EfCoreModel_MapsOnlyControlItOwnedTables()
    {
        var options = new DbContextOptionsBuilder<ControlItDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Database=netlock;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        using var db = new ControlItDbContext(options);
        var tables = db.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        Assert.NotEmpty(tables);
        Assert.All(tables, table =>
            Assert.StartsWith("controlit_", table, StringComparison.Ordinal));
    }
}
