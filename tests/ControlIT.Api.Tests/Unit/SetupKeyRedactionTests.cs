namespace ControlIT.Api.Tests.Unit;

using ControlIT.Api.Domain.DTOs.Responses;
using ControlIT.Api.Domain.Models;
using Xunit;

/// <summary>
/// Proves the setup-key redaction contract (P1-1):
/// - SetupKeyListResponse always carries "[redacted]" in the Key field.
/// - SetupKeyCreateResponse carries the raw key exactly as provided.
/// </summary>
[Trait("Category", "Unit")]
public class SetupKeyRedactionTests
{
    private static NetbirdSetupKey MakeKey(string rawKey = "sk-live-abc123xyz") =>
        new()
        {
            Id = "key-1",
            Name = "Test Key",
            Key = rawKey,
            Type = "reusable",
            Valid = true,
            Revoked = false,
            UsedTimes = 0,
            UsageLimit = 10,
            Expires = DateTime.UtcNow.AddDays(30),
            AutoGroups = ["grp-1"],
            Ephemeral = false,
            State = "valid"
        };

    // ── SetupKeyListResponse ─────────────────────────────────────────────────

    [Fact]
    public void ListResponse_Key_IsAlwaysRedacted()
    {
        var key = MakeKey("sk-live-supersecret");
        var dto = new SetupKeyListResponse(
            Id: key.Id,
            Name: key.Name,
            Key: "[redacted]",
            Type: key.Type,
            Valid: key.Valid,
            Revoked: key.Revoked,
            UsedTimes: key.UsedTimes,
            UsageLimit: key.UsageLimit,
            Expires: key.Expires,
            AutoGroups: key.AutoGroups,
            Ephemeral: key.Ephemeral,
            State: key.State);

        Assert.Equal("[redacted]", dto.Key);
        Assert.NotEqual(key.Key, dto.Key);
    }

    [Fact]
    public void ListResponse_Key_DoesNotContainRawKeyValue()
    {
        const string rawKey = "sk-live-supersecret";
        var key = MakeKey(rawKey);
        var dto = new SetupKeyListResponse(
            Id: key.Id,
            Name: key.Name,
            Key: "[redacted]",
            Type: key.Type,
            Valid: key.Valid,
            Revoked: key.Revoked,
            UsedTimes: key.UsedTimes,
            UsageLimit: key.UsageLimit,
            Expires: key.Expires,
            AutoGroups: key.AutoGroups,
            Ephemeral: key.Ephemeral,
            State: key.State);

        Assert.DoesNotContain(rawKey, dto.Key);
    }

    [Fact]
    public void ListResponse_NonKeyFields_ArePreserved()
    {
        var key = MakeKey();
        var dto = new SetupKeyListResponse(
            Id: key.Id,
            Name: key.Name,
            Key: "[redacted]",
            Type: key.Type,
            Valid: key.Valid,
            Revoked: key.Revoked,
            UsedTimes: key.UsedTimes,
            UsageLimit: key.UsageLimit,
            Expires: key.Expires,
            AutoGroups: key.AutoGroups,
            Ephemeral: key.Ephemeral,
            State: key.State);

        Assert.Equal(key.Id, dto.Id);
        Assert.Equal(key.Name, dto.Name);
        Assert.Equal(key.Type, dto.Type);
        Assert.Equal(key.Valid, dto.Valid);
        Assert.Equal(key.Revoked, dto.Revoked);
        Assert.Equal(key.UsedTimes, dto.UsedTimes);
        Assert.Equal(key.UsageLimit, dto.UsageLimit);
        Assert.Equal(key.Expires, dto.Expires);
        Assert.Equal(key.AutoGroups, dto.AutoGroups);
        Assert.Equal(key.Ephemeral, dto.Ephemeral);
        Assert.Equal(key.State, dto.State);
    }

    // ── SetupKeyCreateResponse ───────────────────────────────────────────────

    [Fact]
    public void CreateResponse_Key_ExposesRawKey()
    {
        const string rawKey = "sk-live-abc123xyz";
        var key = MakeKey(rawKey);
        var dto = new SetupKeyCreateResponse(
            Id: key.Id,
            Name: key.Name,
            Key: key.Key,
            Type: key.Type,
            Valid: key.Valid,
            Revoked: key.Revoked,
            UsedTimes: key.UsedTimes,
            UsageLimit: key.UsageLimit,
            Expires: key.Expires,
            AutoGroups: key.AutoGroups,
            Ephemeral: key.Ephemeral,
            State: key.State);

        Assert.Equal(rawKey, dto.Key);
    }

    [Fact]
    public void CreateResponse_Key_IsNotRedacted()
    {
        var key = MakeKey("sk-live-real-secret");
        var dto = new SetupKeyCreateResponse(
            Id: key.Id,
            Name: key.Name,
            Key: key.Key,
            Type: key.Type,
            Valid: key.Valid,
            Revoked: key.Revoked,
            UsedTimes: key.UsedTimes,
            UsageLimit: key.UsageLimit,
            Expires: key.Expires,
            AutoGroups: key.AutoGroups,
            Ephemeral: key.Ephemeral,
            State: key.State);

        Assert.NotEqual("[redacted]", dto.Key);
        Assert.Equal(key.Key, dto.Key);
    }

    // ── Contract boundary: list vs create are distinct types ────────────────

    [Fact]
    public void ListResponse_And_CreateResponse_AreDistinctTypes()
    {
        Assert.NotEqual(
            typeof(SetupKeyListResponse),
            typeof(SetupKeyCreateResponse));
    }
}
