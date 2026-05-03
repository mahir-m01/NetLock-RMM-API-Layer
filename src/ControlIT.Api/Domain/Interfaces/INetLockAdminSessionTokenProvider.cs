namespace ControlIT.Api.Domain.Interfaces;

public interface INetLockAdminSessionTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
}
