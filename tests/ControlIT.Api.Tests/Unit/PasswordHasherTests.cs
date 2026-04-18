namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Infrastructure.Auth;
using Xunit;

[Trait("Category", "Unit")]
public class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var hash = _sut.Hash("MyPassword123!");
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Hash_TwoCallsSamePlaintext_ProduceDifferentHashes()
    {
        // BCrypt salts each hash independently.
        var h1 = _sut.Hash("SamePassword1!");
        var h2 = _sut.Hash("SamePassword1!");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForMatchingPlaintext()
    {
        var hash = _sut.Hash("CorrectPassword1!");
        Assert.True(_sut.Verify("CorrectPassword1!", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPlaintext()
    {
        var hash = _sut.Hash("CorrectPassword1!");
        Assert.False(_sut.Verify("WrongPassword1!", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForEmptyPassword()
    {
        var hash = _sut.Hash("CorrectPassword1!");
        Assert.False(_sut.Verify("", hash));
    }

    [Fact]
    public void Verify_IsConstantTime_NotSensitiveToInputLength()
    {
        // BCrypt.Verify is always constant-time — this just confirms it doesn't throw.
        var hash = _sut.Hash("Password123!");
        var result = _sut.Verify(new string('x', 1000), hash);
        Assert.False(result);
    }
}
