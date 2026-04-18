using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ControlIT.Api.Infrastructure.Persistence;

// Used only by `dotnet ef migrations` at design time — not registered in DI.
public sealed class ControlItDbContextFactory : IDesignTimeDbContextFactory<ControlItDbContext>
{
    public ControlItDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ControlItDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=netlockrmm;User=root;Password=placeholder;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new ControlItDbContext(options);
    }
}
