namespace ControlIT.Api.Tests.Unit;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public class JwtServiceTests
{
    private const string ValidKey = "test-signing-key-exactly-32-bytes!!";
    private readonly JwtService _sut;

    public JwtServiceTests()
    {
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", ValidKey);
        _sut = new JwtService(NullLogger<JwtService>.Instance);
    }

    private static ControlItUser MakeSuperAdmin() => new()
    {
        Id = 1,
        Email = "admin@test.local",
        PasswordHash = "x",
        Role = Role.SuperAdmin
    };

    private static ControlItUser MakeClientAdmin() => new()
    {
        Id = 2,
        Email = "client@test.local",
        PasswordHash = "x",
        Role = Role.ClientAdmin,
        TenantId = 42
    };

    private static ControlItUser MakeTechnician() => new()
    {
        Id = 3,
        Email = "tech@test.local",
        PasswordHash = "x",
        Role = Role.Technician,
        TenantId = 5,
        AssignedClientsJson = "[10,20]"
    };

    [Fact]
    public void IssueAccessToken_ReturnsNonEmptyJwt()
    {
        var token = _sut.IssueAccessToken(MakeSuperAdmin());
        Assert.NotEmpty(token);
        Assert.Equal(3, token.Split('.').Length); // header.payload.signature
    }

    [Fact]
    public void IssueAccessToken_SuperAdmin_HasCorrectClaims()
    {
        var token = _sut.IssueAccessToken(MakeSuperAdmin());
        var principal = _sut.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal("1", principal!.FindFirstValue("sub"));
        Assert.Equal("SuperAdmin", principal.FindFirstValue("role"));
        Assert.Null(principal.FindFirstValue("tenant_id"));
    }

    [Fact]
    public void IssueAccessToken_ClientAdmin_HasTenantIdClaim()
    {
        var token = _sut.IssueAccessToken(MakeClientAdmin());
        var principal = _sut.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal("ClientAdmin", principal!.FindFirstValue("role"));
        Assert.Equal("42", principal.FindFirstValue("tenant_id"));
    }

    [Fact]
    public void IssueAccessToken_Technician_HasAssignedClientsClaim()
    {
        var token = _sut.IssueAccessToken(MakeTechnician());
        var principal = _sut.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal("Technician", principal!.FindFirstValue("role"));
        Assert.Equal("[10,20]", principal.FindFirstValue("assigned_clients"));
    }

    [Fact]
    public void IssueAccessToken_ExpiresIn15Minutes()
    {
        var before = DateTime.UtcNow;
        var token = _sut.IssueAccessToken(MakeSuperAdmin());
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expiry = jwt.ValidTo;
        Assert.InRange(expiry, before.AddSeconds(890), before.AddSeconds(910)); // ~15 min ± 10s
    }

    [Fact]
    public void ValidateToken_ReturnsNull_ForTamperedToken()
    {
        var token = _sut.IssueAccessToken(MakeSuperAdmin());
        var tampered = token[..^5] + "XXXXX";
        var result = _sut.ValidateToken(tampered);
        Assert.Null(result);
    }

    [Fact]
    public void ValidateToken_ReturnsNull_ForRandomString()
    {
        Assert.Null(_sut.ValidateToken("not.a.jwt"));
    }

    [Fact]
    public void AccessTokenLifetimeSeconds_Is900()
    {
        Assert.Equal(900, _sut.AccessTokenLifetimeSeconds);
    }

    [Fact]
    public void Constructor_ThrowsOnMissingKey()
    {
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", null);
        Assert.Throws<InvalidOperationException>(() =>
            new JwtService(NullLogger<JwtService>.Instance));
        // Restore for other tests.
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", ValidKey);
    }

    [Fact]
    public void Constructor_ThrowsOnKeyTooShort()
    {
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", "short");
        Assert.Throws<InvalidOperationException>(() =>
            new JwtService(NullLogger<JwtService>.Instance));
        Environment.SetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY", ValidKey);
    }

    [Fact]
    public void BuildValidationParameters_RoleClaimTypeIsShortForm()
    {
        var vp = JwtService.BuildValidationParameters(ValidKey);
        Assert.Equal("role", vp.RoleClaimType);
    }
}
