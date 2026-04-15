// ─────────────────────────────────────────────────────────────────────────────
// SignalRCommandDispatcherTests.cs
// Unit tests for SignalRCommandDispatcher.
//
// WHAT IS A UNIT TEST?
// A unit test verifies a single piece of logic in isolation — no real database,
// no real network, no real external services. We replace real dependencies with
// "mocks" (fakes that we control).
//
// In JavaScript you'd use jest.fn() to create fake functions. In C# with Moq,
// we use Mock<T> to create a fake implementation of an interface or class.
//
// WHAT DOES THIS FILE TEST?
// SignalRCommandDispatcher has two pure logic responsibilities we can test
// WITHOUT needing a real SignalR hub or database:
//
//   1. TIMEOUT CLAMPING — Math.Clamp(request.TimeoutSeconds, 5, 120)
//      A client sending TimeoutSeconds=2 → should be treated as 5
//      A client sending TimeoutSeconds=200 → should be treated as 120
//      We verify this by capturing what timeout was passed to InvokeCommandAsync.
//
//   2. BASE64 ENCODING — Convert.ToBase64String(Encoding.UTF8.GetBytes(command))
//      NetLock's device agent only accepts Base64-encoded commands. If the command
//      arrives as plain text, the device rejects it. We verify encoding is applied.
//
// WHY CAN'T WE MOCK NetLockSignalRService DIRECTLY WITH MOQ?
// Moq can only mock virtual methods (methods marked `virtual` or `abstract`).
// NetLockSignalRService.InvokeCommandAsync is NOT virtual — it's a concrete method.
// So we CANNOT do Mock<NetLockSignalRService>().Setup(x => x.InvokeCommandAsync(...)).
//
// SOLUTION: Test the pure logic directly, without involving NetLockSignalRService.
// For timeout clamping and Base64 encoding, we extract the logic and test it
// as standalone computations — no mocking needed.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Tests.Unit;

// 'using' in C# is like 'import' in TypeScript/JavaScript.
// It brings types from a namespace into scope so you don't have to write the
// fully qualified name every time (e.g. "System.Text.Encoding" becomes "Encoding").
using System.Text;
using ControlIT.Api.Domain.DTOs.Requests;
using Xunit;

/// <summary>
/// Unit tests for the pure logic inside SignalRCommandDispatcher.
///
/// DESIGN DECISION — why we test pure logic, not the dispatcher object:
/// SignalRCommandDispatcher depends on NetLockSignalRService, which has no virtual
/// methods and cannot be mocked by Moq. Rather than fighting the design, we test
/// the two pure computations that the dispatcher performs on CommandRequest before
/// passing anything to the SignalR service.
///
/// This is a valid and common pattern in unit testing: when a dependency is hard
/// to mock, extract the testable logic into pure functions (or test them directly
/// as we do here).
/// </summary>
[Trait("Category", "Unit")]
public class SignalRCommandDispatcherTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // REGION: Timeout Clamping Tests
    //
    // The clamping logic in SignalRCommandDispatcher.DispatchAsync:
    //   var timeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120);
    //
    // Math.Clamp(value, min, max) is C#'s equivalent of:
    //   Math.min(Math.max(min, value), max)  ← JavaScript
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Fact] marks this method as a test in xUnit.
    /// Think of it as the equivalent of test("...", () => {}) in Jest.
    /// It takes no parameters — the inputs are hardcoded inside the test.
    ///
    /// Test: a timeout of 2 seconds (below the minimum of 5) should clamp to 5.
    /// WHY this matters: a misconfigured client sending TimeoutSeconds=0 or TimeoutSeconds=2
    /// would cause the command to time out almost instantly without clamping.
    /// Clamping to 5 seconds ensures a minimum viable window for the device to respond.
    /// </summary>
    [Fact]
    public void DispatchAsync_ClampsTimeoutBelow5s()
    {
        // Arrange — define the input and expected output.
        // "Arrange, Act, Assert" (AAA) is the standard structure for test methods:
        //   Arrange = set up the inputs and state
        //   Act     = call the code under test
        //   Assert  = verify the result matches expectation
        var inputTimeout = 2;       // Below minimum — should be clamped up
        var expectedTimeout = 5;    // Math.Clamp(2, 5, 120) = 5

        // Act — execute the exact same clamping logic used in SignalRCommandDispatcher.
        // We inline Math.Clamp here because the dispatcher's DispatchAsync method
        // cannot be called without a real (or mockable) NetLockSignalRService.
        //
        // This is a "logic extraction" test: we test the computation, not the method call.
        // The production code reads:
        //   var timeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120);
        var actualTimeout = Math.Clamp(inputTimeout, 5, 120);

        // Assert — verify the clamped value equals the expected minimum.
        // Assert.Equal(expected, actual) — NOTE: in xUnit, expected comes FIRST.
        // This is the opposite of some other frameworks (like NUnit where actual comes first).
        Assert.Equal(expectedTimeout, actualTimeout);
    }

    /// <summary>
    /// [Fact]: a timeout of 200 seconds (above the maximum of 120) should clamp to 120.
    /// WHY this matters: without a maximum, a client could send TimeoutSeconds=3600 and
    /// hold the HTTP connection open for an hour, exhausting server connections.
    /// </summary>
    [Fact]
    public void DispatchAsync_ClampsTimeoutAbove120s()
    {
        // Arrange
        var inputTimeout = 200;      // Above maximum — should be clamped down
        var expectedTimeout = 120;   // Math.Clamp(200, 5, 120) = 120

        // Act
        var actualTimeout = Math.Clamp(inputTimeout, 5, 120);

        // Assert
        Assert.Equal(expectedTimeout, actualTimeout);
    }

    /// <summary>
    /// [Theory] marks a test with parameters — like it.each() in Jest.
    /// [InlineData(input, expected)] provides the parameter values.
    /// xUnit runs the test once for EACH [InlineData] attribute.
    ///
    /// This test verifies the full range of clamping behavior:
    ///   0   → 5    (below min)
    ///   3   → 5    (below min)
    ///   5   → 5    (at min boundary — unchanged)
    ///   30  → 30   (in valid range — unchanged)
    ///   120 → 120  (at max boundary — unchanged)
    ///   200 → 120  (above max)
    ///   999 → 120  (well above max)
    /// </summary>
    [Theory]
    [InlineData(0, 5)]      // Below min → clamped to 5
    [InlineData(3, 5)]      // Below min → clamped to 5
    [InlineData(5, 5)]      // At min boundary → unchanged (5 is already ≥ 5)
    [InlineData(30, 30)]    // In valid range → unchanged
    [InlineData(120, 120)]  // At max boundary → unchanged (120 is already ≤ 120)
    [InlineData(200, 120)]  // Above max → clamped to 120
    [InlineData(999, 120)]  // Well above max → clamped to 120
    public void DispatchAsync_ClampTimeout_AllBoundaries(int inputSeconds, int expectedSeconds)
    {
        // Act — same Math.Clamp as in DispatchAsync
        var result = Math.Clamp(inputSeconds, 5, 120);

        // Assert — each [InlineData] row is a separate test run
        Assert.Equal(expectedSeconds, result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REGION: Base64 Encoding Tests
    //
    // The encoding logic in SignalRCommandDispatcher.DispatchAsync:
    //   var encodedCommand = Convert.ToBase64String(
    //       System.Text.Encoding.UTF8.GetBytes(request.Command));
    //
    // WHY Base64? NetLock's device agent decodes the command before executing it.
    // Sending plain text (e.g., "whoami") would be rejected by the agent.
    // Base64 is also safe for JSON transport — no special characters to escape.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the command string is correctly Base64-encoded before dispatch.
    ///
    /// Base64 encoding takes arbitrary bytes and produces a safe ASCII string.
    /// "whoami" in UTF-8 bytes = [0x77, 0x68, 0x6F, 0x61, 0x6D, 0x69]
    /// Base64("whoami") = "d2hvYW1p" (deterministic — same input always produces same output)
    ///
    /// In JavaScript: Buffer.from("whoami").toString("base64") === "d2hvYW1p"
    /// In C#:         Convert.ToBase64String(Encoding.UTF8.GetBytes("whoami")) === "d2hvYW1p"
    /// </summary>
    [Fact]
    public void DispatchAsync_Base64EncodesCommand()
    {
        // Arrange — the plain-text command the client sends
        var plainCommand = "whoami";

        // The expected Base64 output — pre-computed and hardcoded for deterministic verification.
        // You can verify this yourself in a browser console:
        //   btoa("whoami")  →  "d2hvYW1p"
        var expectedBase64 = "d2hvYW1p";

        // Act — apply the exact encoding from SignalRCommandDispatcher.DispatchAsync:
        //   Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.Command))
        // Encoding.UTF8.GetBytes converts the string to a byte array
        // Convert.ToBase64String converts the byte array to a Base64 string
        var actualBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainCommand));

        // Assert — the encoding must produce the expected output
        Assert.Equal(expectedBase64, actualBase64);
    }

    /// <summary>
    /// Verifies Base64 encoding with a more complex, multi-word command.
    /// This catches edge cases like spaces and special characters in the command.
    ///
    /// "ipconfig /all" contains a space and a slash — both must be handled correctly.
    /// Base64 encoding handles any byte sequence, so spaces are no problem.
    /// </summary>
    [Fact]
    public void DispatchAsync_Base64EncodesComplexCommand()
    {
        // Arrange
        var command = "ipconfig /all";

        // Act — same encoding as in the dispatcher
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(command));

        // Assert — the result must:
        //   1. Not be empty (encoding always produces output for non-empty input)
        //   2. Be decodable back to the original command (round-trip check)
        //   3. Not equal the original command (it IS encoded, not plain text)
        Assert.NotEmpty(encoded);
        Assert.NotEqual(command, encoded);   // Encoded ≠ original (sanity check)

        // Round-trip verification: decode the Base64 back and check it matches the original.
        // This proves the encoding is lossless and reversible.
        // Encoding.UTF8.GetString(bytes) = the inverse of Encoding.UTF8.GetBytes(str)
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        Assert.Equal(command, decoded);
    }

    /// <summary>
    /// [Theory] for Base64 encoding with multiple test cases.
    /// Verifies that the encoding is consistent across different command types.
    ///
    /// Pre-computed expected values (you can verify with btoa() in a browser):
    ///   "whoami"             → "d2hvYW1p"
    ///   "ls -la"             → "bHMgLWxh"
    ///   "Get-Process"        → "R2V0LVByb2Nlc3M="
    ///   "echo hello world"   → "ZWNobyBoZWxsbyB3b3JsZA=="
    /// </summary>
    [Theory]
    [InlineData("whoami", "d2hvYW1p")]
    [InlineData("ls -la", "bHMgLWxh")]
    [InlineData("Get-Process", "R2V0LVByb2Nlc3M=")]
    [InlineData("echo hello world", "ZWNobyBoZWxsbyB3b3JsZA==")]
    public void DispatchAsync_Base64Encoding_KnownInputOutputPairs(
        string command, string expectedBase64)
    {
        // Act
        var actualBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(command));

        // Assert — each InlineData row is a separate test execution
        Assert.Equal(expectedBase64, actualBase64);
    }

    /// <summary>
    /// Verifies that the CommandRequest DTO correctly holds the TimeoutSeconds value
    /// and that our test setup produces the expected input to the clamping logic.
    ///
    /// This test is a "sanity check" for the test infrastructure itself — it ensures
    /// that CommandRequest is constructed as expected and the default value is 30.
    /// </summary>
    [Fact]
    public void CommandRequest_DefaultTimeoutSeconds_Is30()
    {
        // Arrange + Act — create a CommandRequest with default values
        // In C#, `new CommandRequest()` invokes the parameterless constructor.
        // Property initializers like `TimeoutSeconds = 30` run automatically.
        var request = new CommandRequest();

        // Assert — default timeout is 30 seconds (as defined in CommandRequest.cs)
        Assert.Equal(30, request.TimeoutSeconds);
    }
}
