using ControlIT.Api.Domain.Models;
using System.Security.Claims;

namespace ControlIT.Api.Domain.Interfaces;

public interface IJwtService
{
    string IssueAccessToken(ControlItUser user);
    ClaimsPrincipal? ValidateToken(string token);
    int AccessTokenLifetimeSeconds { get; }
}
