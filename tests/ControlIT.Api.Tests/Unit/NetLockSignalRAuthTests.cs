namespace ControlIT.Api.Tests.Unit;

using System.Text.Json;
using ControlIT.Api.Infrastructure.NetLock;
using Xunit;

[Trait("Category", "Unit")]
public class NetLockSignalRAuthTests
{
    [Fact]
    public void BuildAdminIdentityHeaderValue_UrlEncodesJson_ForNonAsciiToken()
    {
        const string token = "abc\u00AA$11$tail";

        var header = NetLockSignalRService.BuildAdminIdentityHeaderValue(token);
        var decoded = Uri.UnescapeDataString(header);
        using var doc = JsonDocument.Parse(decoded);

        Assert.True(header.All(c => c <= 127));
        Assert.DoesNotContain(token, header);
        Assert.Equal(token, doc.RootElement
            .GetProperty("admin_identity")
            .GetProperty("token")
            .GetString());
    }

    [Fact]
    public void BuildAdminIdentityHeaderValue_UsesNetLockExpectedEnvelope()
    {
        const string token = "test-token";

        var decoded = Uri.UnescapeDataString(
            NetLockSignalRService.BuildAdminIdentityHeaderValue(token));

        Assert.Equal("{\"admin_identity\":{\"token\":\"test-token\"}}", decoded);
    }

    [Fact]
    public void BuildAdminIdentityHeaderValue_EscapesJsonSpecialCharacters()
    {
        const string token = "quote\"slash\\tail";

        var decoded = Uri.UnescapeDataString(
            NetLockSignalRService.BuildAdminIdentityHeaderValue(token));
        using var doc = JsonDocument.Parse(decoded);

        Assert.Equal(token, doc.RootElement
            .GetProperty("admin_identity")
            .GetProperty("token")
            .GetString());
    }
}
