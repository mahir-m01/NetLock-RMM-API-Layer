using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlIT.Api.Infrastructure.Persistence;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ControlItDbContext _db;

    public RefreshTokenRepository(ControlItDbContext db) => _db = db;

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<int> CreateAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return token.Id;
    }

    public async Task RevokeAsync(int id, int? replacedById, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FindAsync([id], ct);
        if (token is null) return;
        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedById = replacedById;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
