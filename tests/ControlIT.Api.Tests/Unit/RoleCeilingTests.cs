namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Models;
using Xunit;

/// <summary>
/// Proves the role ceiling contract:
/// - SuperAdmin may manage CpAdmin and below — NOT another SuperAdmin.
/// - CpAdmin may manage only ClientAdmin and Technician — NOT CpAdmin or SuperAdmin.
/// - ClientAdmin may manage only Technician.
/// - Technician may manage nobody.
/// </summary>
[Trait("Category", "Unit")]
public class RoleCeilingTests
{
    [Theory]
    [InlineData(Role.SuperAdmin, false)]   // SuperAdmin cannot manage another SuperAdmin
    [InlineData(Role.CpAdmin, true)]
    [InlineData(Role.ClientAdmin, true)]
    [InlineData(Role.Technician, true)]
    public void SuperAdmin_ManagementMatrix(Role target, bool expected) =>
        Assert.Equal(expected, RoleCeiling.CanManage(Role.SuperAdmin, target));

    [Theory]
    [InlineData(Role.SuperAdmin, false)]   // equal/higher — blocked
    [InlineData(Role.CpAdmin, false)]      // equal — blocked
    [InlineData(Role.ClientAdmin, true)]   // lower — allowed
    [InlineData(Role.Technician, true)]    // lower — allowed
    public void CpAdmin_ManagementMatrix(Role target, bool expected) =>
        Assert.Equal(expected, RoleCeiling.CanManage(Role.CpAdmin, target));

    [Theory]
    [InlineData(Role.SuperAdmin, false)]
    [InlineData(Role.CpAdmin, false)]
    [InlineData(Role.ClientAdmin, false)]  // equal — blocked
    [InlineData(Role.Technician, true)]
    public void ClientAdmin_ManagementMatrix(Role target, bool expected) =>
        Assert.Equal(expected, RoleCeiling.CanManage(Role.ClientAdmin, target));

    [Theory]
    [InlineData(Role.SuperAdmin, false)]
    [InlineData(Role.CpAdmin, false)]
    [InlineData(Role.ClientAdmin, false)]
    [InlineData(Role.Technician, false)]   // equal — blocked
    public void Technician_CanManage_Nobody(Role target, bool expected) =>
        Assert.Equal(expected, RoleCeiling.CanManage(Role.Technician, target));

    // Regression: the original bug was `<` instead of `<=`, allowing equal-role management.
    // These cases explicitly test the equal-role boundary for CpAdmin.
    [Fact]
    public void CpAdmin_Cannot_Create_EqualRole_CpAdmin() =>
        Assert.False(RoleCeiling.CanManage(Role.CpAdmin, Role.CpAdmin));

    [Fact]
    public void CpAdmin_Cannot_Patch_EqualRole_CpAdmin() =>
        Assert.False(RoleCeiling.CanManage(Role.CpAdmin, Role.CpAdmin));

    [Fact]
    public void CpAdmin_Cannot_ForceReset_EqualRole_CpAdmin() =>
        Assert.False(RoleCeiling.CanManage(Role.CpAdmin, Role.CpAdmin));

    [Fact]
    public void CpAdmin_Cannot_Manage_SuperAdmin() =>
        Assert.False(RoleCeiling.CanManage(Role.CpAdmin, Role.SuperAdmin));
}
