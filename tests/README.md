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
        ├── HealthEndpointTests.cs             - /health exempt from auth, /devices requires auth
        └── EndpointIntegrationTests.cs        - All business endpoints against the live dev stack
```

**Current totals: 40 tests — 40 passing.**

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

Start the full ASP.NET Core application in-process via `WebApplicationFactory`, running the complete middleware pipeline against real infrastructure.

**Prerequisites:**

```bash
colima start --arch aarch64 --vm-type vz --vz-rosetta --cpu 4 --memory 6
docker compose up -d
```

**Test API key:** `controlit-test-2026` (tenant\_id = 3, stored in `controlit_api_keys`)

#### HealthEndpointTests

Auth middleware coverage:

| Test | What it verifies |
|---|---|
| `Health_IsExemptFromAuth` | `GET /health` returns 200 without an API key |
| `Devices_RequiresAuth` | `GET /devices` returns 401 without an API key |

#### EndpointIntegrationTests

Full business endpoint coverage against the live dev stack (MySQL + NetLock hub):

| Test | Endpoint | What it verifies |
|---|---|---|
| `GetDevices_ReturnsOk_WithPagedBody` | `GET /devices?page=1&pageSize=10` | 200 status |
| `GetDevices_Body_HasRequiredPagedFields` | `GET /devices?page=1&pageSize=10` | Body contains `items`, `totalCount`, `page`, `pageSize` |
| `GetDeviceById_ExistingId_ReturnsOk` | `GET /devices/27` | 200 for a device that exists in the DB |
| `GetDeviceById_ExistingId_HasRequiredFields` | `GET /devices/27` | Body has `id == 27`, non-empty `deviceName` and `platform` |
| `GetDeviceById_NonExistentId_Returns404` | `GET /devices/99999` | 404 for a device that does not exist |
| `GetDashboard_ReturnsOk` | `GET /dashboard` | 200 status |
| `GetDashboard_Body_HasRequiredFields` | `GET /dashboard` | Body has `totalDevices`, `onlineDevices`, `totalTenants`, `totalEvents` all >= 0 |
| `GetEvents_ReturnsOk_WithPagedBody` | `GET /events?page=1&pageSize=10` | 200 status |
| `GetEvents_Body_TotalCountIsNonNegative` | `GET /events?page=1&pageSize=10` | `totalCount` >= 0 |
| `GetTenants_ReturnsOk_WithNonNullBody` | `GET /tenants` | 200 with a non-null array body |
| `GetAuditLogs_ReturnsOk_WithArrayBody` | `GET /audit/logs?limit=10&offset=0` | 200 with an array body (may be empty) |
| `PostCommandsExecute_Device27_ReturnsValidStatus` | `POST /commands/execute` | Returns 200, 503, or 504 — never 400/401/404/500 |
| `GetDashboard_WithoutApiKey_Returns401` | `GET /dashboard` (no key) | 401 when the `x-api-key` header is absent |

`POST /commands/execute` accepts three status codes because the outcome depends on whether device 27 is online at the time the test runs:

- `200` - device online, command executed
- `503` - device or hub offline
- `504` - device reachable but command timed out

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
        _client.DefaultRequestHeaders.Add("x-api-key", "controlit-test-2026");
    }

    [Fact]
    public async Task Endpoint_Condition_ExpectedOutcome()
    {
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

**Device IDs are environment-specific.**
`GetDeviceById_ExistingId_*` tests target device ID 27, which exists in the local dev MySQL instance. If the database is reset or reprovisioned, update the ID to match a real device in the new dataset.

---

## CI

To run only unit tests in CI (no infrastructure required):

```yaml
- name: Test
  run: dotnet test ControlIT.Api.sln --filter "Category=Unit" --no-build
```

To include integration tests, provision a MySQL service and a NetLock hub, or use a dedicated test environment with the dev stack pre-running.
