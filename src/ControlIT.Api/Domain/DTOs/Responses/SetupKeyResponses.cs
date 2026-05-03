// ─────────────────────────────────────────────────────────────────────────────
// SetupKeyResponses.cs
// Two distinct response shapes for Netbird setup-key operations.
//
// WHY two separate types:
//   Setup keys are reusable enrollment secrets. Returning the raw key on every
//   list call would allow any authenticated user (including read-only roles) to
//   harvest active enrollment keys indefinitely after creation.
//
//   The raw key must appear at most once — in the creation response — and never
//   again in any subsequent read or list response.
//
// SetupKeyListResponse  — returned by GET /network/setup-keys.
//                         Key field is always "[redacted]" regardless of caller role.
//
// SetupKeyCreateResponse — returned by POST /network/setup-keys only.
//                          Carries the raw key for one-time display to the creator.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Domain.DTOs.Responses;

/// <summary>
/// Safe list shape for a Netbird setup key. The Key field is always redacted.
/// </summary>
public record SetupKeyListResponse(
    string Id,
    string Name,
    string Key,
    string Type,
    bool Valid,
    bool Revoked,
    int UsedTimes,
    int UsageLimit,
    DateTime Expires,
    List<string> AutoGroups,
    bool Ephemeral,
    string State);

/// <summary>
/// Create response that carries the raw key exactly once.
/// Returned only by POST /network/setup-keys. Must not be cached or re-exposed.
/// </summary>
public record SetupKeyCreateResponse(
    string Id,
    string Name,
    string Key,
    string Type,
    bool Valid,
    bool Revoked,
    int UsedTimes,
    int UsageLimit,
    DateTime Expires,
    List<string> AutoGroups,
    bool Ephemeral,
    string State);
