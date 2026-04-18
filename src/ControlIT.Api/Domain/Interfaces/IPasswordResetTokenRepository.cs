using ControlIT.Api.Domain.Models;

namespace ControlIT.Api.Domain.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<int> CreateAsync(PasswordResetToken token, CancellationToken ct = default);
    Task MarkUsedAsync(int id, CancellationToken ct = default);
}
