using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlIT.Api.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly ControlItDbContext _db;

    public UserRepository(ControlItDbContext db) => _db = db;

    public Task<ControlItUser?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<ControlItUser?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<IReadOnlyList<ControlItUser>> ListAsync(CancellationToken ct = default)
        => await _db.Users.OrderBy(u => u.Email).ToListAsync(ct);

    public async Task<int> CreateAsync(ControlItUser user, CancellationToken ct = default)
    {
        user.Email = user.Email.ToLowerInvariant();
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user.Id;
    }

    public Task UpdateAsync(ControlItUser user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        return _db.SaveChangesAsync(ct);
    }

    public Task<bool> AnyExistsAsync(CancellationToken ct = default)
        => _db.Users.AnyAsync(ct);
}
