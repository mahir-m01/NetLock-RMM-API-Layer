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

    // The ONLY table managed by this context.
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    // Maps AuditEntry to the controlit_audit_log table with explicit column names and indexes.
    // After running "dotnet ef migrations add", verify the generated SQL touches ONLY
    // controlit_* tables.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntry>(entity =>
        {
            // Explicit table name — EF would default to "AuditEntries" without this.
            entity.ToTable("controlit_audit_log");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            // Timestamp is set by the application layer (DateTime.UtcNow).
            // Pomelo's generated DEFAULT UTC_TIMESTAMP() syntax is invalid on MySQL 8.0,
            // so HasDefaultValueSql is intentionally omitted.

            // Explicit snake_case column names — EF defaults to PascalCase, which would
            // break Dapper queries that expect snake_case column names.
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.ActorKeyId).HasColumnName("actor_key_id").HasMaxLength(16);
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(64);
            entity.Property(e => e.ResourceType).HasColumnName("resource_type").HasMaxLength(64);
            entity.Property(e => e.ResourceId).HasColumnName("resource_id").HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);  // IPv6 max length
            entity.Property(e => e.Result).HasColumnName("result").HasMaxLength(16);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");

            // (tenant_id, timestamp) covers filtered date-range queries per tenant.
            // (actor_key_id) covers "who did what" queries.
            entity.HasIndex(e => new { e.TenantId, e.Timestamp })
                  .HasDatabaseName("idx_audit_tenant_time");
            entity.HasIndex(e => e.ActorKeyId)
                  .HasDatabaseName("idx_audit_actor");
        });
    }
}
