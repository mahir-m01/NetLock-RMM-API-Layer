// ─────────────────────────────────────────────────────────────────────────────
// ControlItDbContext.cs
// Pattern: EF Core DbContext — the Unit of Work and Identity Map for
// ControlIT's own tables. ONLY `controlit_*` tables live here.
//
// WHY two ORMs: EF Core manages tables that ControlIT OWNS (can create/alter).
// Dapper reads tables that NetLock OWNS (we must not alter or migrate).
// Mixing the two in a single context would risk accidentally migrating
// NetLock tables — so this DbContext is intentionally restricted to
// `controlit_*` tables only.
//
// ORM boundary: NEVER add a DbSet<Device>, DbSet<Tenant>, or any other
// NetLock model to this context. EF migrations must never touch non-controlit tables.
// ─────────────────────────────────────────────────────────────────────────────
namespace ControlIT.Api.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ControlIT.Api.Domain.Models;

public class ControlItDbContext : DbContext
{
    public ControlItDbContext(DbContextOptions<ControlItDbContext> options) : base(options) { }

    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<ControlItUser> Users => Set<ControlItUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("controlit_audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ActorKeyId).HasColumnName("actor_key_id").HasMaxLength(16);
            entity.Property(e => e.ActorEmail).HasColumnName("actor_email").HasMaxLength(255);
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(64);
            entity.Property(e => e.ResourceType).HasColumnName("resource_type").HasMaxLength(64);
            entity.Property(e => e.ResourceId).HasColumnName("resource_id").HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.Result).HasColumnName("result").HasMaxLength(16);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.HasIndex(e => new { e.TenantId, e.Timestamp }).HasDatabaseName("idx_audit_tenant_time");
            entity.HasIndex(e => e.ActorKeyId).HasDatabaseName("idx_audit_actor");
        });

        modelBuilder.Entity<ControlItUser>(entity =>
        {
            entity.ToTable("controlit_users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(72).IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasConversion<int>();
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.AssignedClientsJson).HasColumnName("assigned_clients");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(e => e.PasswordChangedAt).HasColumnName("password_changed_at");
            entity.Property(e => e.FailedLoginCount).HasColumnName("failed_login_count");
            entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            // Case-insensitive uniqueness enforced at the application layer (email is lowercased before storage).
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("idx_users_email");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("controlit_refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("idx_refresh_token_hash");
            entity.HasOne<ControlItUser>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("controlit_password_reset_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("idx_reset_token_hash");
            entity.HasOne<ControlItUser>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
