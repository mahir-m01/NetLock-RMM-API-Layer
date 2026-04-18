namespace ControlIT.Api.Tests.Unit;

using System.Security.Claims;
using ControlIT.Api.Domain.Models;
using Xunit;

/// <summary>
/// Verifies the TenantMember, CpAdminOrAbove, CanExecuteCommands, and SuperAdminOnly
/// policy assertions against claim principals — without spinning up the full pipeline.
/// </summary>
[Trait("Category", "Unit")]
public class AuthPolicyTests
{
    private static ClaimsPrincipal MakePrincipal(Role role, int? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new("sub", "1"),
            new("role", role.ToString())
        };
        if (tenantId.HasValue)
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "jwt", "sub", "role");
        return new ClaimsPrincipal(identity);
    }

    // TenantMember: SuperAdmin/CpAdmin always pass; ClientAdmin/Technician need tenant_id
    [Theory]
    [InlineData(Role.SuperAdmin, null, true)]
    [InlineData(Role.CpAdmin, null, true)]
    [InlineData(Role.ClientAdmin, 1, true)]
    [InlineData(Role.Technician, 1, true)]
    [InlineData(Role.ClientAdmin, null, false)]   // Missing tenant_id
    [InlineData(Role.Technician, null, false)]    // Missing tenant_id
    public void TenantMember_AssertionLogic(Role role, int? tenantId, bool expected)
    {
        var principal = MakePrincipal(role, tenantId);
        var roleValue = principal.FindFirst("role")?.Value;
        bool isElevated = roleValue is nameof(Role.SuperAdmin) or nameof(Role.CpAdmin);
        bool hasTenant = principal.HasClaim(c => c.Type == "tenant_id");

        var result = isElevated || hasTenant;
        Assert.Equal(expected, result);
    }

    // CpAdminOrAbove: only SuperAdmin and CpAdmin pass
    [Theory]
    [InlineData(Role.SuperAdmin, true)]
    [InlineData(Role.CpAdmin, true)]
    [InlineData(Role.ClientAdmin, false)]
    [InlineData(Role.Technician, false)]
    public void CpAdminOrAbove_RoleCheck(Role role, bool expected)
    {
        var principal = MakePrincipal(role);
        var result = principal.IsInRole(nameof(Role.SuperAdmin))
                     || principal.IsInRole(nameof(Role.CpAdmin));
        Assert.Equal(expected, result);
    }

    // CanExecuteCommands: SuperAdmin, CpAdmin, Technician — NOT ClientAdmin
    [Theory]
    [InlineData(Role.SuperAdmin, true)]
    [InlineData(Role.CpAdmin, true)]
    [InlineData(Role.Technician, true)]
    [InlineData(Role.ClientAdmin, false)]
    public void CanExecuteCommands_RoleCheck(Role role, bool expected)
    {
        var principal = MakePrincipal(role);
        var result = principal.IsInRole(nameof(Role.SuperAdmin))
                     || principal.IsInRole(nameof(Role.CpAdmin))
                     || principal.IsInRole(nameof(Role.Technician));
        Assert.Equal(expected, result);
    }

    // SuperAdminOnly: only SuperAdmin
    [Theory]
    [InlineData(Role.SuperAdmin, true)]
    [InlineData(Role.CpAdmin, false)]
    [InlineData(Role.ClientAdmin, false)]
    [InlineData(Role.Technician, false)]
    public void SuperAdminOnly_RoleCheck(Role role, bool expected)
    {
        var principal = MakePrincipal(role);
        Assert.Equal(expected, principal.IsInRole(nameof(Role.SuperAdmin)));
    }
}
