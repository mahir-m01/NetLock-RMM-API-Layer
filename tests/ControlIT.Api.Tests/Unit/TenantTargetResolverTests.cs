namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Application;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Moq;
using Xunit;

[Trait("Category", "Unit")]
public class TenantTargetResolverTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TenantContext TenantContextFor(Role role, int? tenantId)
    {
        var actor = new Mock<IActorContext>();
        actor.Setup(a => a.Role).Returns(role);
        actor.Setup(a => a.TenantId).Returns(tenantId);
        return new TenantContext(actor.Object);
    }

    private static ITenantRepository RepoReturning(int id, Tenant? tenant)
    {
        var repo = new Mock<ITenantRepository>();
        repo.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync(tenant);
        return repo.Object;
    }

    private static ITenantRepository RepoReturningNotFound()
    {
        var repo = new Mock<ITenantRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Tenant?)null);
        return repo.Object;
    }

    private static Tenant SomeTenant(int id) => new() { Id = id, Name = "T" };

    // ── Elevated role (SuperAdmin / CpAdmin) ──────────────────────────────

    [Fact]
    public async Task ElevatedRole_WithoutTargetTenantId_Returns400()
    {
        var tenant = TenantContextFor(Role.SuperAdmin, null);
        var repo = new Mock<ITenantRepository>().Object;

        var result = await TenantTargetResolver.ResolveAsync(tenant, null, repo);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("targetTenantId is required", result.Error);
    }

    [Fact]
    public async Task ElevatedRole_WithValidTargetTenantId_ReturnsResolvedId()
    {
        var tenant = TenantContextFor(Role.CpAdmin, null);
        var repo = RepoReturning(5, SomeTenant(5));

        var result = await TenantTargetResolver.ResolveAsync(tenant, 5, repo);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.TenantId);
    }

    [Fact]
    public async Task ElevatedRole_WithInvalidTenantId_Returns400()
    {
        var tenant = TenantContextFor(Role.SuperAdmin, null);
        var repo = RepoReturningNotFound();

        var result = await TenantTargetResolver.ResolveAsync(tenant, 99, repo);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("99", result.Error);
        Assert.Contains("not found", result.Error);
    }

    // ── Scoped role (ClientAdmin / Technician) ────────────────────────────

    [Fact]
    public async Task TenantRole_WithNullTargetTenantId_ReturnsOwnId()
    {
        var tenant = TenantContextFor(Role.ClientAdmin, 7);
        var repo = new Mock<ITenantRepository>().Object;

        var result = await TenantTargetResolver.ResolveAsync(tenant, null, repo);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.TenantId);
    }

    [Fact]
    public async Task TenantRole_WithMatchingTargetTenantId_ReturnsOwnId()
    {
        var tenant = TenantContextFor(Role.Technician, 3);
        var repo = new Mock<ITenantRepository>().Object;

        var result = await TenantTargetResolver.ResolveAsync(tenant, 3, repo);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.TenantId);
    }

    [Fact]
    public async Task TenantRole_WithDifferentTargetTenantId_Returns403()
    {
        var tenant = TenantContextFor(Role.ClientAdmin, 3);
        var repo = new Mock<ITenantRepository>().Object;

        var result = await TenantTargetResolver.ResolveAsync(tenant, 99, repo);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("Cross-tenant access denied", result.Error);
    }

    [Fact]
    public async Task UnresolvedTenantRole_Returns400()
    {
        // ClientAdmin with no TenantId is a misconfigured JWT — IsResolved == false
        var tenant = TenantContextFor(Role.ClientAdmin, null);
        var repo = new Mock<ITenantRepository>().Object;

        var result = await TenantTargetResolver.ResolveAsync(tenant, null, repo);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("No tenant context available", result.Error);
    }

    // ── Resolution matrix (Theory) ────────────────────────────────────────
    //
    // Columns: role, ownTenantId, targetTenantId, tenantExistsInDb,
    //          expectedSuccess, expectedTenantId, expectedStatus
    //
    // Rows exercise the full cross-product of elevated/scoped × valid/invalid target.

    [Theory]
    // Elevated — no target → 400
    [InlineData(Role.SuperAdmin, null, null, false, false, null, 400)]
    [InlineData(Role.CpAdmin, null, null, false, false, null, 400)]
    // Elevated — target not in DB → 400
    [InlineData(Role.SuperAdmin, null, 10, false, false, null, 400)]
    [InlineData(Role.CpAdmin, null, 10, false, false, null, 400)]
    // Elevated — target exists → success
    [InlineData(Role.SuperAdmin, null, 10, true, true, 10, 200)]
    [InlineData(Role.CpAdmin, null, 10, true, true, 10, 200)]
    // Scoped — no target → own ID
    [InlineData(Role.ClientAdmin, 4, null, false, true, 4, 200)]
    [InlineData(Role.Technician, 4, null, false, true, 4, 200)]
    // Scoped — matching target → own ID (target ignored safely)
    [InlineData(Role.ClientAdmin, 4, 4, false, true, 4, 200)]
    [InlineData(Role.Technician, 4, 4, false, true, 4, 200)]
    // Scoped — cross-tenant target → 403
    [InlineData(Role.ClientAdmin, 4, 9, false, false, null, 403)]
    [InlineData(Role.Technician, 4, 9, false, false, null, 403)]
    // Scoped unresolved (misconfigured JWT) → 400
    [InlineData(Role.ClientAdmin, null, null, false, false, null, 400)]
    public async Task ResolutionMatrix(
        Role role,
        int? ownTenantId,
        int? targetTenantId,
        bool tenantExistsInDb,
        bool expectedSuccess,
        int? expectedTenantId,
        int expectedStatus)
    {
        var tenant = TenantContextFor(role, ownTenantId);

        var repoMock = new Mock<ITenantRepository>();
        if (targetTenantId.HasValue)
        {
            var returnValue = tenantExistsInDb ? SomeTenant(targetTenantId.Value) : null;
            repoMock
                .Setup(r => r.GetByIdAsync(targetTenantId.Value, default))
                .ReturnsAsync(returnValue);
        }

        var result = await TenantTargetResolver.ResolveAsync(tenant, targetTenantId, repoMock.Object);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        Assert.Equal(expectedTenantId, result.TenantId);
        Assert.Equal(expectedStatus, result.StatusCode);
    }
}
