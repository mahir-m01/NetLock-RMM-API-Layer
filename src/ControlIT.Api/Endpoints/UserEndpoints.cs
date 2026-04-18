using System.Text.Json;
using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Endpoints;

public static class UserEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /users — list all users (CpAdminOrAbove)
        app.MapGet("/users", async (IUserRepository users) =>
        {
            var list = await users.ListAsync();
            return Results.Ok(list.Select(ToSummary));
        }).RequireAuthorization("CpAdminOrAbove").RequireRateLimiting("api");

        // POST /users — create user, return generated password once
        app.MapPost("/users", async (
            CreateUserRequest request,
            IUserRepository users,
            IPasswordHasher hasher,
            IAuditService audit,
            IActorContext actor) =>
        {
            var password = GeneratePassword();
            var user = new ControlItUser
            {
                Email = request.Email,
                PasswordHash = hasher.Hash(password),
                Role = request.Role,
                TenantId = request.TenantId,
                AssignedClientsJson = request.AssignedClients is not null
                    ? JsonSerializer.Serialize(request.AssignedClients)
                    : null,
                MustChangePassword = true,
                PasswordChangedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var id = await users.CreateAsync(user);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = actor.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "USER_CREATE",
                ResourceType = "User",
                ResourceId = id.ToString(),
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.Ok(new { Id = id, Email = user.Email, Role = user.Role, GeneratedPassword = password });
        }).RequireAuthorization("CpAdminOrAbove").RequireRateLimiting("api");

        // GET /users/{id} — CpAdminOrAbove OR self
        app.MapGet("/users/{id:int}", async (
            int id,
            IUserRepository users,
            IActorContext actor) =>
        {
            var isSelf = actor.UserId == id;
            var isElevated = actor.Role is Role.SuperAdmin or Role.CpAdmin;

            if (!isSelf && !isElevated)
                return Results.Forbid();

            var user = await users.GetByIdAsync(id);
            return user is null ? Results.NotFound() : Results.Ok(ToSummary(user));
        }).RequireAuthorization().RequireRateLimiting("api");

        // PATCH /users/{id} — CpAdminOrAbove
        app.MapPatch("/users/{id:int}", async (
            int id,
            UpdateUserRequest request,
            IUserRepository users,
            IAuditService audit,
            IActorContext actor) =>
        {
            var user = await users.GetByIdAsync(id);
            if (user is null) return Results.NotFound();

            if (request.Role.HasValue) user.Role = request.Role.Value;
            if (request.TenantId.HasValue) user.TenantId = request.TenantId;
            if (request.AssignedClients is not null)
                user.AssignedClientsJson = JsonSerializer.Serialize(request.AssignedClients);
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

            await users.UpdateAsync(user);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = actor.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "USER_UPDATE",
                ResourceType = "User",
                ResourceId = id.ToString(),
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.Ok(ToSummary(user));
        }).RequireAuthorization("CpAdminOrAbove").RequireRateLimiting("api");

        // DELETE /users/{id} — SuperAdminOnly; soft-delete (sets IsActive = false)
        app.MapDelete("/users/{id:int}", async (
            int id,
            IUserRepository users,
            IAuditService audit,
            IActorContext actor) =>
        {
            var user = await users.GetByIdAsync(id);
            if (user is null) return Results.NotFound();

            if (actor.UserId == id)
                return Results.Problem(detail: "Cannot deactivate your own account.", statusCode: 400);

            user.IsActive = false;
            await users.UpdateAsync(user);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = actor.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "USER_DEACTIVATE",
                ResourceType = "User",
                ResourceId = id.ToString(),
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.NoContent();
        }).RequireAuthorization("SuperAdminOnly").RequireRateLimiting("api");

        // POST /users/{id}/force-password-reset — CpAdminOrAbove
        app.MapPost("/users/{id:int}/force-password-reset", async (
            int id,
            IUserRepository users,
            IAuditService audit,
            IActorContext actor) =>
        {
            var user = await users.GetByIdAsync(id);
            if (user is null) return Results.NotFound();

            user.MustChangePassword = true;
            await users.UpdateAsync(user);

            await audit.RecordAsync(new AuditEntry
            {
                TenantId = actor.TenantId ?? 0,
                ActorKeyId = actor.UserId.ToString(),
                ActorEmail = actor.Email,
                Action = "USER_FORCE_PASSWORD_RESET",
                ResourceType = "User",
                ResourceId = id.ToString(),
                IpAddress = actor.IpAddress,
                Result = "SUCCESS"
            });

            return Results.NoContent();
        }).RequireAuthorization("CpAdminOrAbove").RequireRateLimiting("api");
    }

    private static object ToSummary(ControlItUser user) => new
    {
        user.Id,
        user.Email,
        Role = user.Role.ToString(),
        user.TenantId,
        user.IsActive,
        user.MustChangePassword,
        user.CreatedAt,
        user.LastLoginAt
    };

    private static string GeneratePassword()
    {
        // Generates a 16-char password that satisfies the policy (upper + lower + digit + symbol).
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";

        var rand = new Random();
        var chars = new List<char>
        {
            upper[rand.Next(upper.Length)],
            lower[rand.Next(lower.Length)],
            digits[rand.Next(digits.Length)],
            symbols[rand.Next(symbols.Length)]
        };

        var all = upper + lower + digits + symbols;
        for (int i = 4; i < 16; i++)
            chars.Add(all[rand.Next(all.Length)]);

        return new string(chars.OrderBy(_ => rand.Next()).ToArray());
    }
}
