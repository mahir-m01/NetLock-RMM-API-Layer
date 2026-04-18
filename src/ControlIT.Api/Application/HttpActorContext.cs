using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.AspNetCore.Http;

namespace ControlIT.Api.Application;

public sealed class HttpActorContext : IActorContext
{
    private readonly IHttpContextAccessor _http;

    public HttpActorContext(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal User => _http.HttpContext?.User
        ?? throw new InvalidOperationException("No HTTP context available.");

    public int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("JWT is missing 'sub' claim."));

    public Role Role
    {
        get
        {
            var raw = User.FindFirstValue("role")
                ?? throw new InvalidOperationException("JWT is missing 'role' claim.");
            return Enum.Parse<Role>(raw, ignoreCase: true);
        }
    }

    public int? TenantId
    {
        get
        {
            var raw = User.FindFirstValue("tenant_id");
            return raw is null ? null : int.Parse(raw);
        }
    }

    public IReadOnlyList<int> AssignedClients
    {
        get
        {
            var json = User.FindFirstValue("assigned_clients");
            if (string.IsNullOrWhiteSpace(json)) return [];
            return JsonSerializer.Deserialize<List<int>>(json) ?? [];
        }
    }

    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string Email =>
        User.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? User.FindFirstValue(ClaimTypes.Email)
        ?? throw new InvalidOperationException("JWT is missing 'email' claim.");
}
