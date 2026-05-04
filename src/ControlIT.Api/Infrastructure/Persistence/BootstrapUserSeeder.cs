using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlIT.Api.Infrastructure.Persistence;

public sealed class BootstrapUserSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BootstrapUserSeeder> _logger;

    public BootstrapUserSeeder(IServiceProvider services, ILogger<BootstrapUserSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
        => await SeedAsync(_services, _logger, forceSync: false, ct);

    public static async Task SeedAsync(
        IServiceProvider services,
        ILogger logger,
        bool forceSync,
        CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = Environment.GetEnvironmentVariable("CONTROLIT_BOOTSTRAP_EMAIL")
                    ?? "admin@controlit.local";
        var password = Environment.GetEnvironmentVariable("CONTROLIT_BOOTSTRAP_PASSWORD");
        var existing = await users.GetByEmailAsync(email, ct);

        if (existing is not null)
        {
            if (!forceSync) return;

            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("CONTROLIT_BOOTSTRAP_PASSWORD is required to sync bootstrap user.");

            existing.PasswordHash = hasher.Hash(password);
            existing.Role = Role.SuperAdmin;
            existing.IsActive = true;
            existing.MustChangePassword = true;
            existing.FailedLoginCount = 0;
            existing.LockedUntil = null;
            existing.PasswordChangedAt = DateTime.UtcNow;
            await users.UpdateAsync(existing, ct);

            logger.LogInformation(
                "Bootstrap SuperAdmin synced: {Email}. Must change password on next login.",
                email);
            return;
        }

        if (await users.AnyExistsAsync(ct) && !forceSync) return;

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogCritical(
                "No users exist and CONTROLIT_BOOTSTRAP_PASSWORD is not set. " +
                "Set this environment variable and restart.");
            throw new InvalidOperationException("CONTROLIT_BOOTSTRAP_PASSWORD is required for first-run setup.");
        }

        var user = new ControlItUser
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            Role = Role.SuperAdmin,
            MustChangePassword = true,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await users.CreateAsync(user, ct);

        logger.LogInformation(
            "Bootstrap SuperAdmin created: {Email}. Must change password on first login.",
            email);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
