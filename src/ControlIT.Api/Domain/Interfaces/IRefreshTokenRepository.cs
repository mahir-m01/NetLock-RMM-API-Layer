using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<int> CreateAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAsync(int id, int? replacedById, CancellationToken ct = default);
    Task RevokeAllForUserAsync(int userId, CancellationToken ct = default);
}
