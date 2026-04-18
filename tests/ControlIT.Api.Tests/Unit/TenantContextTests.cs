namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Moq;
using Xunit;

[Trait("Category", "Unit")]
public class TenantContextTests
{
    private static TenantContext For(Role role, int? tenantId)
    {
        var actor = new Mock<IActorContext>();
        actor.Setup(a => a.Role).Returns(role);
        actor.Setup(a => a.TenantId).Returns(tenantId);
        return new TenantContext(actor.Object);
    }

    [Fact]
    public void IsAllTenants_True_ForSuperAdmin()
    {
        var ctx = For(Role.SuperAdmin, null);
        Assert.True(ctx.IsAllTenants);
        Assert.True(ctx.IsResolved);
        Assert.Null(ctx.TenantId);
    }

    [Fact]
    public void IsAllTenants_True_ForCpAdmin()
    {
        var ctx = For(Role.CpAdmin, null);
        Assert.True(ctx.IsAllTenants);
        Assert.True(ctx.IsResolved);
    }

    [Fact]
    public void IsAllTenants_False_ForClientAdmin_WithTenantId()
    {
        var ctx = For(Role.ClientAdmin, 42);
        Assert.False(ctx.IsAllTenants);
        Assert.True(ctx.IsResolved);
        Assert.Equal(42, ctx.TenantId);
    }

    [Fact]
    public void IsAllTenants_False_ForTechnician_WithTenantId()
    {
        var ctx = For(Role.Technician, 7);
        Assert.False(ctx.IsAllTenants);
        Assert.True(ctx.IsResolved);
        Assert.Equal(7, ctx.TenantId);
    }

    [Fact]
    public void IsResolved_False_ForClientAdmin_WithNullTenantId()
    {
        // A ClientAdmin without tenantId is a misconfigured user — not resolved.
        var ctx = For(Role.ClientAdmin, null);
        Assert.False(ctx.IsAllTenants);
        Assert.False(ctx.IsResolved);
    }

    [Theory]
    [InlineData(Role.SuperAdmin, null, true, true)]
    [InlineData(Role.CpAdmin, null, true, true)]
    [InlineData(Role.ClientAdmin, 1, false, true)]
    [InlineData(Role.Technician, 5, false, true)]
    [InlineData(Role.ClientAdmin, null, false, false)]
    public void TenantContext_ResolutionMatrix(Role role, int? tenantId, bool expectedAllTenants, bool expectedResolved)
    {
        var ctx = For(role, tenantId);
        Assert.Equal(expectedAllTenants, ctx.IsAllTenants);
        Assert.Equal(expectedResolved, ctx.IsResolved);
    }
}
