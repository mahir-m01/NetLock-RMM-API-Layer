# ControlIT API — Testing Guide

> Written for someone coming from TypeScript/Jest. Every concept is explained from scratch.
> If you already know a concept, skip the "What is X?" boxes and go straight to the code.

---

## Table of Contents

1. [What is xUnit? (The Jest of C#)](#1-what-is-xunit-the-jest-of-c)
2. [Project Structure](#2-project-structure)
3. [How to Run Tests](#3-how-to-run-tests)
4. [The Two Types of Tests in This Project](#4-the-two-types-of-tests-in-this-project)
5. [Unit Tests — SignalRCommandDispatcherTests](#5-unit-tests--signalrcommanddispatchertests)
6. [Unit Tests — TenantContextTests](#6-unit-tests--tenantcontexttests)
7. [Integration Tests — HealthEndpointTests](#7-integration-tests--healthendpointtests)
8. [Why Some Tests Need the Dev Stack Running](#8-why-some-tests-need-the-dev-stack-running)
9. [Writing New Tests — Cheat Sheet](#9-writing-new-tests--cheat-sheet)
10. [Known Caveats and Limitations](#10-known-caveats-and-limitations)

---

## 1. What is xUnit? (The Jest of C#)

In JavaScript you use **Jest**. In C# the equivalent is **xUnit**.

| Concept | Jest (TypeScript) | xUnit (C#) |
|---|---|---|
| Mark a function as a test | `test("name", () => {})` | `[Fact]` attribute on a method |
| Parameterized test (multiple inputs) | `it.each(...)` | `[Theory]` + `[InlineData(...)]` |
| Expect a value | `expect(x).toBe(y)` | `Assert.Equal(expected, actual)` |
| Expect true/false | `expect(x).toBeTruthy()` | `Assert.True(x)` / `Assert.False(x)` |
| Expect not equal | `expect(x).not.toBe(y)` | `Assert.NotEqual(expected, actual)` |
| Expect not null/empty | `expect(x).toBeDefined()` | `Assert.NotNull(x)` / `Assert.NotEmpty(x)` |
| Setup before all tests | `beforeAll(() => {})` | `IClassFixture<T>` interface |
| Setup before each test | `beforeEach(() => {})` | Constructor of the test class |
| Mock a function/class | `jest.fn()` / `jest.mock()` | `new Mock<T>()` from Moq |
| Run tests | `npx jest` | `dotnet test` |

### The Arrange / Act / Assert Pattern

Every test in this project follows the **AAA** pattern — the same as you'd use in Jest:

```
// JavaScript Jest
test("clamps timeout below 5", () => {
    // Arrange
    const input = 2;
    // Act
    const result = clamp(input, 5, 120);
    // Assert
    expect(result).toBe(5);
});
```

```csharp
// C# xUnit
[Fact]
public void ClampsTimeoutBelow5s()
{
    // Arrange
    var input = 2;
    // Act
    var result = Math.Clamp(input, 5, 120);
    // Assert
    Assert.Equal(5, result);
}
```

> **One important difference:** in xUnit, `Assert.Equal(expected, actual)` — the **expected value goes first**. This is the opposite of how you might write it in English ("the actual value should equal the expected"), but it's the xUnit convention. Getting it backwards doesn't break tests, but it makes failure messages confusing.

---

## 2. Project Structure

```
test-code/ControlIT.Api/
│
├── ControlIT.Api.sln               ← Solution file — ties both projects together
│                                     (like a monorepo root package.json)
│
├── src/
│   └── ControlIT.Api/              ← The actual API (DO NOT touch these files)
│       ├── ControlIT.Api.csproj
│       ├── Program.cs
│       └── ...
│
└── tests/
    └── ControlIT.Api.Tests/        ← The test project (everything here is yours to edit)
        ├── ControlIT.Api.Tests.csproj
        │
        ├── Unit/
        │   ├── SignalRCommandDispatcherTests.cs   ← Tests for timeout + Base64 logic
        │   └── TenantContextTests.cs              ← Tests for IsResolved / TenantId
        │
        └── Integration/
            └── HealthEndpointTests.cs             ← Tests for /health and /devices endpoints
```

### What is a .sln file?

A `.sln` (Solution) file is like a workspace config. It tells `dotnet` which projects belong together. When you run `dotnet test` from the solution directory, it finds and runs tests from all test projects listed in the `.sln`.

You don't edit `.sln` files by hand — use `dotnet sln add` to add new projects.

### What is a .csproj file?

A `.csproj` is the C# equivalent of `package.json`. It defines:
- Which .NET version to use (`TargetFramework`)
- Which packages to install (`PackageReference` — like `dependencies` in package.json)
- Which other projects this project references (`ProjectReference` — like workspace dependencies)

---

## 3. How to Run Tests

All commands run from the **solution root**:

```bash
cd /Users/mahir/Code/NetLock-RMM-API-Layer/test-code/ControlIT.Api
```

### Run all tests

```bash
dotnet test
```

### Run only unit tests (no dev stack needed)

```bash
dotnet test --filter "Category=Unit"
```

Unit tests have no external dependencies — they run purely in memory. No MySQL, no NetLock, no Docker needed.

### Run only integration tests (dev stack must be running)

```bash
dotnet test --filter "Category=Integration"
```

Integration tests start the full API in-memory, which connects to MySQL and NetLock's SignalR hub at startup. If those aren't running, the tests will fail with a connection error.

### Run with verbose output (see each test name and result)

```bash
dotnet test --logger "console;verbosity=normal"
```

### Run a single test by name

```bash
dotnet test --filter "FullyQualifiedName~TenantContextTests"
dotnet test --filter "FullyQualifiedName~DispatchAsync_ClampsTimeoutBelow5s"
```

### Expected output (unit tests only)

```
Build succeeded.

Starting test execution...

Passed! - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 0.3s
```

---

## 4. The Two Types of Tests in This Project

### Unit Tests

Test **a single piece of logic** in complete isolation. No real database, no real network, no real external service. If a dependency is needed, you replace it with a **mock** (a fake controlled by your test).

```
Your test code
     ↓
The class you're testing
     ↓ (calls)
A mock (fake) instead of the real dependency
```

Unit tests:
- Run in milliseconds
- Work without any external services running
- Tell you exactly which piece of logic broke

### Integration Tests

Test the **full stack end-to-end**. They start the real ASP.NET Core application (in-memory, no actual port) and make real HTTP requests through the full middleware pipeline.

```
Your test code
     ↓ (HTTP request)
ASP.NET Core pipeline (middleware, routing, auth)
     ↓
The endpoint handler
     ↓ (calls)
Real MySQL, Real SignalR
```

Integration tests:
- Take longer (seconds, not milliseconds) because they start the full app
- Require external services (MySQL, NetLock hub) to be running
- Catch bugs that unit tests can't: middleware ordering, DI misconfiguration, SQL errors

---

## 5. Unit Tests — SignalRCommandDispatcherTests

**File:** `tests/ControlIT.Api.Tests/Unit/SignalRCommandDispatcherTests.cs`

These tests verify two pure logic operations inside `SignalRCommandDispatcher.DispatchAsync`:

### What's being tested

**1. Timeout clamping**

In `SignalRCommandDispatcher.cs`, the dispatcher clamps the timeout to 5–120 seconds:
```csharp
var timeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120);
```

Without clamping, a client could send `timeoutSeconds: 0` (instant timeout) or `timeoutSeconds: 9999` (hold the connection open for hours). The clamp prevents both.

**2. Base64 encoding**

NetLock's device agent requires commands to be Base64-encoded:
```csharp
var encodedCommand = Convert.ToBase64String(
    System.Text.Encoding.UTF8.GetBytes(request.Command));
```

If you send `"whoami"` as plain text, the agent rejects it. Encoded: `"d2hvYW1p"`. The tests verify this encoding is applied correctly and is reversible (you can decode it back to the original).

### Why we can't mock NetLockSignalRService

> This is a common C# gotcha that trips people up.

In Jest, you can mock any module:
```javascript
jest.mock('./NetLockSignalRService');
```

In C#, Moq can only mock:
- **Interfaces** (e.g., `ICommandDispatcher`)
- **Abstract classes**
- **Classes with `virtual` methods**

`NetLockSignalRService.InvokeCommandAsync` is a regular (non-virtual) method. Moq cannot intercept it. So instead of fighting the design, we test the pure computations directly — the math of clamping and the encoding algorithm — without ever calling `InvokeCommandAsync`.

### The [Fact] tests

```csharp
[Fact]
public void DispatchAsync_ClampsTimeoutBelow5s()
```

`[Fact]` = a single test with no parameters. Like `test("name", () => {...})` in Jest.

```csharp
[Fact]
public void DispatchAsync_ClampsTimeoutAbove120s()
```

Tests the upper bound — 200 → 120.

### The [Theory] tests

```csharp
[Theory]
[InlineData(0,   5)]
[InlineData(3,   5)]
[InlineData(5,   5)]
[InlineData(30,  30)]
[InlineData(120, 120)]
[InlineData(200, 120)]
[InlineData(999, 120)]
public void DispatchAsync_ClampTimeout_AllBoundaries(int inputSeconds, int expectedSeconds)
```

`[Theory]` + `[InlineData]` = parameterized test. xUnit runs this method **7 times**, once per `[InlineData]` row. Each row is a separate test in the output.

In Jest this would be:
```javascript
it.each([
    [0,   5],
    [3,   5],
    // ...
])("clamps %d to %d", (input, expected) => {
    expect(Math.clamp(input, 5, 120)).toBe(expected);
});
```

### The Base64 tests

```csharp
[Fact]
public void DispatchAsync_Base64EncodesCommand()
{
    var plainCommand = "whoami";
    var expectedBase64 = "d2hvYW1p";

    var actualBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainCommand));

    Assert.Equal(expectedBase64, actualBase64);
}
```

You can verify the expected value in your browser console:
```javascript
btoa("whoami")  // → "d2hvYW1p"
```

The round-trip test:
```csharp
var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
Assert.Equal(command, decoded);  // original → encode → decode → original ✓
```

---

## 6. Unit Tests — TenantContextTests

**File:** `tests/ControlIT.Api.Tests/Unit/TenantContextTests.cs`

These test the `TenantContext` class — the object that carries the authenticated tenant's ID through every HTTP request.

### What is TenantContext?

Think of `TenantContext` as the C# equivalent of a React Context value or `AsyncLocalStorage` in Node.js. It's created fresh for every HTTP request by the DI container, then populated by `ApiKeyMiddleware` after it validates the API key.

```
HTTP Request arrives
    ↓
DI creates TenantContext (TenantId = 0, IsResolved = false)
    ↓
ApiKeyMiddleware looks up the API key in the DB
    ↓
Sets tenantContext.TenantId = 3  (now IsResolved = true)
    ↓
Every repository/service in this request can read TenantId = 3
```

The key invariant: `IsResolved` is `true` only when `TenantId > 0`.

### Why these tests matter (security)

If `IsResolved` defaulted to `true`, repositories would run queries without a valid tenant, potentially returning cross-tenant data. These tests pin the security guarantee: fresh context = not resolved = repositories throw before touching the DB.

### Test: IsResolved starts false

```csharp
[Fact]
public void IsResolved_ReturnsFalse_WhenDefault()
{
    var tenantContext = new TenantContext();  // DI creates this fresh per request

    Assert.Equal(0, tenantContext.TenantId);  // Starts at 0
    Assert.False(tenantContext.IsResolved);    // 0 > 0 is false → not resolved
}
```

### Test: Parameterized resolution check

```csharp
[Theory]
[InlineData(1,   true)]   // Valid tenant ID → resolved
[InlineData(42,  true)]   // Valid tenant ID → resolved
[InlineData(999, true)]   // Valid tenant ID → resolved
[InlineData(0,   false)]  // Default → NOT resolved
[InlineData(-1,  false)]  // Invalid → NOT resolved
public void IsResolved_MatchesTenantIdSign(int tenantId, bool expectedResolved)
```

This covers the boundaries: any positive integer is "resolved", zero and negatives are not.

### Test: Instance independence (critical security test)

```csharp
[Fact]
public void TenantContext_TwoInstances_AreIndependent()
{
    var contextForRequestA = new TenantContext();
    var contextForRequestB = new TenantContext();

    contextForRequestA.TenantId = 1;
    contextForRequestB.TenantId = 2;

    Assert.Equal(1, contextForRequestA.TenantId);  // A still = 1
    Assert.Equal(2, contextForRequestB.TenantId);  // B still = 2 (not contaminated by A)
}
```

This test catches a critical DI misconfiguration bug: if `TenantContext` were accidentally registered as a **Singleton** (shared across all requests) instead of **Scoped** (one per request), both instances would point to the same object, and setting TenantId on request A would overwrite it for request B. That would be a tenant data leak.

---

## 7. Integration Tests — HealthEndpointTests

**File:** `tests/ControlIT.Api.Tests/Integration/HealthEndpointTests.cs`

> **These require the dev stack to be running.** See [Section 8](#8-why-some-tests-need-the-dev-stack-running) if they fail.

### What is WebApplicationFactory?

`WebApplicationFactory<T>` is an ASP.NET Core testing utility that starts your **entire application in-memory** — the full middleware pipeline, DI container, routing, everything — without binding to a real port. It gives you an `HttpClient` that talks to this in-memory server.

```
Your test
   ↓
HttpClient (configured automatically by WebApplicationFactory)
   ↓ (in-memory, no real network)
Full ASP.NET Core pipeline
   ↓
ErrorHandlingMiddleware → CorsMiddleware → ApiKeyMiddleware → RateLimiter → Endpoint
   ↓
Real MySQL + Real SignalR (because we use Development config)
```

### The anchor type problem

`WebApplicationFactory<T>` requires a type `T` from the API assembly. It uses `T` only to find **which assembly to load** — it never calls any methods on `T`.

The obvious choice would be `Program`, but ASP.NET Core's top-level statements generate an **internal** class called `Program`. Internal classes can't be referenced from a separate test project.

The solution: use any **public non-static class** from the API assembly as the anchor type. We use `TenantContext`:

```csharp
// TenantContext is public, non-static — it works as an anchor type
public class ControlItWebApplicationFactory : WebApplicationFactory<TenantContext>
```

`WebApplicationFactory` finds the API assembly through `TenantContext`'s assembly, then discovers and runs `Program.cs` normally.

### IClassFixture — starting the app once

```csharp
public class HealthEndpointTests : IClassFixture<ControlItWebApplicationFactory>
```

`IClassFixture<T>` tells xUnit: create one instance of `ControlItWebApplicationFactory` and share it across all tests in this class. Starting the app (connecting to MySQL, connecting to SignalR) takes time — you don't want to do it before every single test.

In Jest, the equivalent is:
```javascript
let app;
beforeAll(async () => { app = await startApp(); });
afterAll(async () => { await app.close(); });
```

### Test: /health returns 200 without an API key

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task GetHealth_ReturnsOk_WithoutApiKey()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/health");

    // /health must NOT return 401 (that would mean auth middleware blocked it)
    Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);

    // Must be either 200 (healthy) or 503 (services down) — never 404 or 500
    var validHealthCodes = new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable };
    Assert.Contains(response.StatusCode, validHealthCodes);
}
```

`[Trait("Category", "Integration")]` tags this test. It's how `--filter "Category=Integration"` works — it matches on this trait.

### Test: /devices returns 401 without an API key

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task GetDevices_Returns401_WithoutApiKey()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/devices?page=1&pageSize=5");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

No `X-API-Key` header → `ApiKeyMiddleware` intercepts the request before it ever reaches the endpoint and returns `401`.

---

## 8. Why Some Tests Need the Dev Stack Running

When `WebApplicationFactory` starts the app, it runs `Program.cs` from start to finish — including:

1. `NetLockSchemaValidator.ValidateRequiredColumnsAsync()` — connects to MySQL and checks required columns exist
2. `NetLockSignalRService.StartAsync()` — connects to the NetLock SignalR hub

If MySQL isn't running, the factory throws `MySqlException: Unable to connect` before any test runs. This is expected behavior — the integration tests are testing the **real app against real infrastructure**.

### Checklist before running integration tests

```bash
# 1. Confirm Colima/Docker is running
docker ps | grep mysql-container   # should show "Up"
docker ps | grep netlock-rmm       # should show "Up"

# 2. Run integration tests
cd /Users/mahir/Code/NetLock-RMM-API-Layer/test-code/ControlIT.Api
dotnet test --filter "Category=Integration"
```

### Running unit tests without the dev stack

Unit tests have no external dependencies — they always work:

```bash
dotnet test --filter "Category=Unit"
# ✓ Runs 25 tests, no DB or network needed
```

### CI/CD consideration

If you later add CI (GitHub Actions etc.), you have two options:
1. **Skip integration tests in CI** — add `--filter "Category!=Integration"` to the CI command
2. **Spin up MySQL in CI** — use GitHub Actions' `services` block to start a MySQL container alongside the tests

---

## 9. Writing New Tests — Cheat Sheet

### Adding a unit test

1. Create a new file in `tests/ControlIT.Api.Tests/Unit/` named `YourClassTests.cs`
2. Use this template:

```csharp
namespace ControlIT.Api.Tests.Unit;

using Xunit;

public class YourClassTests
{
    [Fact]
    public void MethodName_WhenCondition_ExpectedResult()
    {
        // Arrange
        var input = ...;

        // Act
        var result = ...;

        // Assert
        Assert.Equal(expected, result);
    }
}
```

### Adding an integration test

1. Add a new test method to `HealthEndpointTests.cs`, or create a new file in `Integration/`
2. Always tag with `[Trait("Category", "Integration")]`
3. Use `_factory.CreateClient()` to get an HttpClient

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task MyEndpoint_ReturnsExpectedStatus()
{
    var client = _factory.CreateClient();

    // Add auth header for protected endpoints:
    client.DefaultRequestHeaders.Add("X-API-Key", "controlit-dev-key-a1428666e451134c");

    var response = await client.GetAsync("/my-endpoint");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### Reading the response body

```csharp
var response = await client.GetAsync("/dashboard");
var json = await response.Content.ReadAsStringAsync();

// Or deserialize to a typed object:
var result = await response.Content.ReadFromJsonAsync<DashboardResponse>();
Assert.Equal(1, result!.TotalDevices);
```

### Common assertions

```csharp
Assert.Equal(expected, actual)           // Values are equal
Assert.NotEqual(unexpected, actual)      // Values are NOT equal
Assert.True(condition)                   // Condition is true
Assert.False(condition)                  // Condition is false
Assert.Null(value)                       // Value is null
Assert.NotNull(value)                    // Value is not null
Assert.Empty(collection)                 // Collection has 0 items
Assert.NotEmpty(collection)              // Collection has ≥1 items
Assert.Contains(item, collection)        // Collection contains item
Assert.Throws<ExceptionType>(() => ...)  // Code throws a specific exception
```

---

## 10. Known Caveats and Limitations

### Caveat 1 — NetLockSignalRService can't be mocked

`NetLockSignalRService.InvokeCommandAsync` is not virtual, so Moq cannot mock it. This means:
- We can't write a unit test that calls `SignalRCommandDispatcher.DispatchAsync` end-to-end
- We test the pure logic (clamping, encoding) directly instead
- If you want full unit test coverage of `DispatchAsync`, you'd need to either:
  - Extract an `INetLockHubClient` interface and have `NetLockSignalRService` implement it
  - Mark `InvokeCommandAsync` as `virtual`

Neither change is made here because it would require modifying the API project, and the pure logic tests cover the most important behaviours.

### Caveat 2 — Integration tests fail if dev stack is down

If you run `dotnet test` without filtering and the dev stack isn't running, you'll see:

```
System.Exception: The application may have exited due to an exception.
MySqlConnector.MySqlException: Unable to connect to any of the specified hosts.
```

This is expected. Run `dotnet test --filter "Category=Unit"` to skip integration tests.

### Caveat 3 — Program.cs is internal (the anchor type workaround)

ASP.NET Core's top-level statements generate an `internal class Program`. The test project uses `TenantContext` as the anchor type for `WebApplicationFactory` because it's public. This is a known limitation and a widely used workaround. If Microsoft adds a `<InternalsVisibleTo>` fix in a future SDK version, you'd be able to use `Program` directly.

To make `Program` visible without modifying source code, you could also add this to `ControlIT.Api.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="ControlIT.Api.Tests" />
</ItemGroup>
```
But we deliberately don't touch the API project, so `TenantContext` as anchor type is the right call here.

### Caveat 4 — Test isolation for integration tests

The integration tests share one `WebApplicationFactory` instance via `IClassFixture`. This means:
- The app starts once before all tests in the class run
- If one test modifies shared state (e.g., writes to the DB), it could affect subsequent tests
- Currently this isn't a problem — the two integration tests only READ (GET requests)
- If you add tests that write data (POST /commands/execute, etc.), consider using `IAsyncLifetime` to clean up after each test

### Caveat 5 — The `[Trait]` filter is case-sensitive

```bash
dotnet test --filter "Category=Integration"   # ✓ Works
dotnet test --filter "category=integration"   # ✗ No tests found (wrong case)
```

---

*Last updated: 2026-04-15*
