using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Application;

/// <summary>
/// Enforces the RBAC role ceiling contract:
/// an actor may only manage users whose role is strictly less privileged (higher int value).
/// SuperAdmin may manage CpAdmin and below, but not other SuperAdmins.
/// </summary>
public static class RoleCeiling
{
    /// <summary>
    /// Returns true when <paramref name="actorRole"/> has permission to create,
    /// patch, deactivate, or force-reset a user with <paramref name="targetRole"/>.
    /// </summary>
    public static bool CanManage(Role actorRole, Role targetRole) =>
        actorRole == Role.SuperAdmin
            ? targetRole != Role.SuperAdmin
            : (int)targetRole > (int)actorRole;
}
