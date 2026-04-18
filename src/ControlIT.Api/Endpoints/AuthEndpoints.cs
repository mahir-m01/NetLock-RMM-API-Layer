using ControlIT.Api.Domain.DTOs.Requests;
using ControlIT.Api.Domain.Interfaces;

namespace ControlIT.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshCookie = "refresh_token";

    public static void Map(WebApplication app)
    {
        // POST /auth/login — anonymous; issues access token + httpOnly refresh cookie
        app.MapPost("/auth/login", async (
            LoginRequest request,
            IAuthService auth,
            HttpContext ctx) =>
        {
            try
            {
                var response = await auth.LoginAsync(request, ctx);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 401, title: "Unauthorized");
            }
        }).AllowAnonymous().RequireRateLimiting("api");

        // POST /auth/refresh — reads refresh token from httpOnly cookie
        app.MapPost("/auth/refresh", async (
            IAuthService auth,
            HttpContext ctx) =>
        {
            var token = ctx.Request.Cookies[RefreshCookie];
            if (string.IsNullOrWhiteSpace(token))
                return Results.Problem(detail: "Refresh token cookie missing.", statusCode: 401, title: "Unauthorized");

            try
            {
                var response = await auth.RefreshAsync(token, ctx);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 401, title: "Unauthorized");
            }
        }).AllowAnonymous().RequireRateLimiting("api");

        // POST /auth/logout — revokes the presented refresh token
        app.MapPost("/auth/logout", async (
            IAuthService auth,
            IActorContext actor,
            HttpContext ctx) =>
        {
            var token = ctx.Request.Cookies[RefreshCookie] ?? string.Empty;
            await auth.LogoutAsync(actor.UserId, token);

            ctx.Response.Cookies.Delete(RefreshCookie);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("api");

        // POST /auth/change-password — authenticated; revokes all other sessions on success
        app.MapPost("/auth/change-password", async (
            ChangePasswordRequest request,
            IAuthService auth,
            IActorContext actor) =>
        {
            try
            {
                await auth.ChangePasswordAsync(actor.UserId, request);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 401, title: "Unauthorized");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 400, title: "Bad Request");
            }
        }).RequireAuthorization().RequireRateLimiting("api");

        // POST /auth/forgot-password — always returns 204; logs reset link
        app.MapPost("/auth/forgot-password", async (
            ForgotPasswordRequest request,
            IAuthService auth) =>
        {
            await auth.RequestPasswordResetAsync(request.Email);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("api");

        // POST /auth/reset-password — anonymous; token from email link
        app.MapPost("/auth/reset-password", async (
            ResetPasswordRequest request,
            IAuthService auth) =>
        {
            try
            {
                await auth.ResetPasswordAsync(request);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 401, title: "Unauthorized");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 400, title: "Bad Request");
            }
        }).AllowAnonymous().RequireRateLimiting("api");

        // GET /auth/me — returns the current user's profile
        app.MapGet("/auth/me", (IActorContext actor) =>
        {
            return Results.Ok(new
            {
                actor.UserId,
                Role = actor.Role.ToString(),
                actor.TenantId,
                actor.AssignedClients
            });
        }).RequireAuthorization().RequireRateLimiting("api");
    }
}
