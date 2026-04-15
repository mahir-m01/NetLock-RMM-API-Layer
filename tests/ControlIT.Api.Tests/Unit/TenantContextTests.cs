// ─────────────────────────────────────────────────────────────────────────────
// TenantContextTests.cs
// Unit tests for the TenantContext class.
//
// WHAT IS TENANTCONTEXT?
// TenantContext is a scoped, per-request object that carries the authenticated
// tenant's ID through the entire HTTP request pipeline. It's like React Context
// or AsyncLocalStorage in TypeScript — a value set once at the top and read
// by many services below without being passed as an explicit parameter.
//
// WHAT WE'RE TESTING:
// 1. IsResolved returns false when TenantId has not been set (default = 0)
// 2. IsResolved returns true after TenantId is set to a positive value
//
// SECURITY CONTEXT:
// TenantId is ONLY set by ApiKeyMiddleware, from a database lookup.
// These tests confirm the invariant: no accidental or default "resolution".
//
// FRAMEWORK NOTES:
// - [Fact] = a single, parameterless test (like test("...", () => {}) in Jest)
// - [Theory] + [InlineData] = parameterized test (like it.each() in Jest)
// - Assert.Equal(expected, actual) — expected always goes FIRST in xUnit
// - Assert.True(condition) / Assert.False(condition) = boolean assertions
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Tests.Unit;

// Import the namespace that contains TenantContext.
// Without this line, the compiler would not know where "TenantContext" comes from.
using ControlIT.Api.Application;
using Xunit;

/// <summary>
/// Unit tests for TenantContext.
/// TenantContext is a pure POCO (Plain Old CLR Object — no base class, no framework magic),
/// so all tests are pure in-memory with no mocking needed.
/// </summary>
[Trait("Category", "Unit")]
public class TenantContextTests
{
    /// <summary>
    /// Verifies that a brand-new TenantContext (as the DI container creates per request)
    /// has IsResolved = false.
    ///
    /// WHY THIS MATTERS: If IsResolved defaulted to true, repositories would execute
    /// database queries without a valid tenant ID — returning data for tenant 0 (which
    /// doesn't exist) or, worse, allowing unauthenticated cross-tenant queries.
    ///
    /// ARRANGE/ACT/ASSERT:
    ///   Arrange = create a new TenantContext
    ///   Act     = read IsResolved (no method to call — property is auto-computed)
    ///   Assert  = IsResolved is false, TenantId is 0
    /// </summary>
    [Fact]
    public void IsResolved_ReturnsFalse_WhenDefault()
    {
        // Arrange + Act — a fresh TenantContext always starts unresolved.
        // In production, the DI container calls `new TenantContext()` at the start of
        // each HTTP request and passes it to ApiKeyMiddleware.
        var tenantContext = new TenantContext();

        // Assert — default TenantId is 0 (the unresolved sentinel value)
        // Assert.Equal(expected, actual) — note expected comes FIRST (xUnit convention)
        Assert.Equal(0, tenantContext.TenantId);

        // Assert.False(condition) — verifies the condition evaluates to false.
        // TenantContext.IsResolved is defined as: TenantId > 0
        // Since TenantId = 0, IsResolved must be false.
        Assert.False(tenantContext.IsResolved);
    }

    /// <summary>
    /// Verifies that IsResolved becomes true after TenantId is assigned a positive value.
    ///
    /// In production, ApiKeyMiddleware does exactly this:
    ///   tenantContext.TenantId = tenantId.Value;  ← result of the DB lookup
    ///
    /// After this assignment, every repository can safely access tenantContext.TenantId
    /// and trust it represents a real, authenticated tenant.
    /// </summary>
    [Fact]
    public void IsResolved_ReturnsTrue_WhenTenantIdSet()
    {
        // Arrange — create a fresh, unresolved context
        var tenantContext = new TenantContext();

        // Act — simulate what ApiKeyMiddleware does after a successful DB lookup.
        // In C#, `{ get; set; }` creates a public getter and setter.
        // This is equivalent to: tenantContext.tenantId = 42 in TypeScript.
        tenantContext.TenantId = 42;

        // Assert — after assigning TenantId = 42, IsResolved must be true.
        // IsResolved is a computed property: `=> TenantId > 0`
        // 42 > 0 = true ✓
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(42, tenantContext.TenantId);
    }

    /// <summary>
    /// [Theory] + [InlineData]: parameterized test — runs once per [InlineData] entry.
    /// In Jest this would be: it.each([[1, true], [100, true], [0, false], [-1, false]])
    ///
    /// Verifies IsResolved for a range of TenantId values:
    ///   - Any positive value (1, 42, 999) → IsResolved = true
    ///   - Zero (0) → IsResolved = false (default, unresolved)
    ///   - Negative values (-1) → IsResolved = false (invalid/corrupt state)
    /// </summary>
    [Theory]
    [InlineData(1, true)]      // Smallest valid TenantId → resolved
    [InlineData(42, true)]     // Typical TenantId → resolved
    [InlineData(999, true)]    // Large TenantId → resolved
    [InlineData(0, false)]     // Default value → NOT resolved
    [InlineData(-1, false)]    // Invalid/corrupt TenantId → NOT resolved
    public void IsResolved_MatchesTenantIdSign(int tenantId, bool expectedResolved)
    {
        // Arrange
        var tenantContext = new TenantContext();

        // Act
        tenantContext.TenantId = tenantId;

        // Assert — the bool we expect matches what IsResolved computes (TenantId > 0)
        Assert.Equal(expectedResolved, tenantContext.IsResolved);
    }

    /// <summary>
    /// Verifies that TenantContext is mutable — TenantId can be set and read back correctly.
    /// This is important because DI creates TenantContext BEFORE ApiKeyMiddleware sets TenantId.
    ///
    /// The DI container creates the instance first (TenantId = 0), then passes it to middleware,
    /// which sets TenantId from the DB. If TenantId were read-only (init-only), this flow
    /// would not work.
    /// </summary>
    [Fact]
    public void TenantId_IsSettable_AndReadable()
    {
        // Arrange
        var tenantContext = new TenantContext();

        // Act — verify we can write and read TenantId
        tenantContext.TenantId = 7;

        // Assert — the value we wrote must come back unchanged
        Assert.Equal(7, tenantContext.TenantId);
    }

    /// <summary>
    /// Verifies that each TenantContext instance is independent.
    /// In production, ASP.NET Core's Scoped DI creates ONE instance per request.
    /// This test confirms two separate instances don't share state — they're not singletons.
    ///
    /// WHY this matters: if two concurrent requests each get the same TenantContext instance
    /// (because it was accidentally registered as Singleton), Tenant A's requests could see
    /// Tenant B's TenantId. This is a critical security bug called "tenant data leakage".
    /// </summary>
    [Fact]
    public void TenantContext_TwoInstances_AreIndependent()
    {
        // Arrange — two separate instances, like two concurrent HTTP requests
        var contextForRequestA = new TenantContext();
        var contextForRequestB = new TenantContext();

        // Act — set different TenantIds on each instance
        contextForRequestA.TenantId = 1;   // Request A authenticated as tenant 1
        contextForRequestB.TenantId = 2;   // Request B authenticated as tenant 2

        // Assert — the two contexts are independent; setting one does NOT affect the other
        Assert.Equal(1, contextForRequestA.TenantId);
        Assert.Equal(2, contextForRequestB.TenantId);

        // Both should be resolved (both > 0)
        Assert.True(contextForRequestA.IsResolved);
        Assert.True(contextForRequestB.IsResolved);
    }
}
