# Testing

## Stack

| Tool | Role |
|---|---|
| xUnit 2.9 | Test runner and assertion library |
| Moq 4.20 | Mock/stub creation for unit tests |
| WebApplicationFactory | In-process integration test host |

---

## Structure

```
tests/
└── ControlIT.Api.Tests/
    ├── Unit/
    │   ├── SignalRCommandDispatcherTests.cs   - Timeout clamping, Base64 encoding
    │   └── TenantContextTests.cs              - IsResolved invariants, instance isolation
    └── Integration/
        └── HealthEndpointTests.cs             - /health exempt from auth, /devices requires auth
```

---

## Running Tests

```bash
# All tests
dotnet test ControlIT.Api.sln

# Unit tests only (no external dependencies)
dotnet test ControlIT.Api.sln --filter "Category=Unit"

# Integration tests only (requires dev stack)
dotnet test ControlIT.Api.sln --filter "Category=Integration"

# Verbose output
dotnet test ControlIT.Api.sln --logger "console;verbosity=normal"

# Single test class
dotnet test ControlIT.Api.sln --filter "FullyQualifiedName~TenantContextTests"
```

---

## Test Categories

### Unit Tests

No external dependencies. Run anywhere, in milliseconds.

Tagged with `[Trait("Category", "Unit")]`. Cover pure logic that can be exercised without infrastructure:

- Timeout clamping bounds (`Math.Clamp(input, 5, 120)`)
- Base64 encoding of command payloads
- `TenantContext.IsResolved` invariants
- `TenantContext` instance isolation (guards against Singleton misconfiguration)

### Integration Tests

Require the dev stack (MySQL + NetLock SignalR hub). Tagged with `[Trait("Category", "Integration")]`.

Start the full ASP.NET Core application in-process via `WebApplicationFactory`, running the complete middleware pipeline against real infrastructure. Cover:

- `ApiKeyMiddleware` exempts `/health` from authentication
- `ApiKeyMiddleware` rejects unauthenticated requests to protected endpoints with 401

**Prerequisites:**

```bash
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
docker compose up -d
```

---

## Writing Tests

### Unit test template

```csharp
namespace ControlIT.Api.Tests.Unit;

[Trait("Category", "Unit")]
public class YourClassTests
{
    [Fact]
    public void MethodName_Condition_ExpectedOutcome()
    {
        // Arrange
        var input = ...;

        // Act
        var result = ...;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0,   5)]
    [InlineData(120, 120)]
    [InlineData(999, 120)]
    public void MethodName_ParameterisedCase(int input, int expected)
    {
        Assert.Equal(expected, Math.Clamp(input, 5, 120));
    }
}
```

### Integration test template

```csharp
namespace ControlIT.Api.Tests.Integration;

[Trait("Category", "Integration")]
public class YourEndpointTests : IClassFixture<ControlItWebApplicationFactory>
{
    private readonly HttpClient _client;

    public YourEndpointTests(ControlItWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Endpoint_Condition_ExpectedOutcome()
    {
        _client.DefaultRequestHeaders.Add("x-api-key", "your-test-key");

        var response = await _client.GetAsync("/your-endpoint");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### Deserialising response bodies

```csharp
var result = await response.Content.ReadFromJsonAsync<DeviceResponse>();
Assert.NotNull(result);
Assert.Equal(27, result.Id);
```

---

## Known Constraints

**`NetLockSignalRService` cannot be mocked.**
`InvokeCommandAsync` is not virtual. Moq requires virtual or interface methods. Tests for `SignalRCommandDispatcher` cover the pure logic (clamping, encoding) rather than the full dispatch path. To achieve full unit coverage, extract an `INetLockHubClient` interface from `NetLockSignalRService`.

**`Program` is internal.**
Top-level statement compilation generates an internal `Program` class. Integration tests use `TenantContext` as the `WebApplicationFactory<T>` anchor type. Alternatively, add `<InternalsVisibleTo Include="ControlIT.Api.Tests" />` to `ControlIT.Api.csproj`.

**Integration tests share one application instance.**
`IClassFixture` starts the app once per test class. Tests that write to the database can affect subsequent tests in the same class. Use `IAsyncLifetime` for per-test setup and teardown if write tests are added.

**Filter matching is case-sensitive.**
`--filter "Category=Unit"` works. `--filter "category=unit"` finds nothing.

---

## CI

To run only unit tests in CI (no infrastructure required):

```yaml
- name: Test
  run: dotnet test ControlIT.Api.sln --filter "Category=Unit" --no-build
```

To include integration tests, provision a MySQL service and a NetLock hub, or use a dedicated test environment with the dev stack pre-running.
