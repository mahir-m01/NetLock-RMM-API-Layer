using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.Interfaces;

public interface IUserRepository
{
    Task<ControlItUser?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ControlItUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<ControlItUser>> ListAsync(CancellationToken ct = default);
    Task<int> CreateAsync(ControlItUser user, CancellationToken ct = default);
    Task UpdateAsync(ControlItUser user, CancellationToken ct = default);
    Task<bool> AnyExistsAsync(CancellationToken ct = default);
}
