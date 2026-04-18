using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlIT.Api.Infrastructure.Persistence;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ControlItDbContext _db;

    public PasswordResetTokenRepository(ControlItDbContext db) => _db = db;

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<int> CreateAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return token.Id;
    }

    public async Task MarkUsedAsync(int id, CancellationToken ct = default)
    {
        var token = await _db.PasswordResetTokens.FindAsync([id], ct);
        if (token is null) return;
        token.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
