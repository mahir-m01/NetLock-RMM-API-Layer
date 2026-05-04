namespace ControlIT.Api.Tests.Unit;

using System.Text.RegularExpressions;
using Xunit;

[Trait("Category", "Unit")]
public class NetLockBoundaryTests
{
    private static readonly Regex VendorWriteSql = new(
        @"\b(INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+(?!controlit_)[`""]?[a-zA-Z_][a-zA-Z0-9_]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Theory]
    [InlineData("src/ControlIT.Api/Infrastructure/Persistence/MySqlDeviceRepository.cs")]
    [InlineData("src/ControlIT.Api/Infrastructure/Persistence/MySqlEventRepository.cs")]
    [InlineData("src/ControlIT.Api/Infrastructure/Persistence/MySqlTenantRepository.cs")]
    [InlineData("src/ControlIT.Api/Infrastructure/NetLock/NetLockSignalRService.cs")]
    public void NetLockBoundary_DoesNotWriteVendorTables(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

        Assert.DoesNotMatch(VendorWriteSql, source);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src/ControlIT.Api")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
