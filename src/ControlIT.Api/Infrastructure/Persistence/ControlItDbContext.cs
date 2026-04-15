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
    // DbContextOptions<T> is passed by the DI container (configured in Program.cs
    // via builder.Services.AddDbContext<ControlItDbContext>(...)).
    // This constructor signature is required by EF Core for DI injection.
    public ControlItDbContext(DbContextOptions<ControlItDbContext> options) : base(options) { }

    // DbSet<T> is how you declare a table in EF Core.
    // This is the ONLY table managed by this context — controlit_audit_log.
    // The `=> Set<AuditEntry>()` pattern is a property accessor (not a field).
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    // OnModelCreating configures the database schema for EF Core migrations.
    // Everything here maps to the controlit_audit_log table.
    // Verify after running "dotnet ef migrations add" that the generated SQL
    // touches ONLY controlit_* tables — if not, stop and investigate.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntry>(entity =>
        {
            // Map to the specific table name — EF would default to "AuditEntries" otherwise.
            entity.ToTable("controlit_audit_log");

            // Primary key — EF needs to know which column is the PK.
            entity.HasKey(e => e.Id);

            // AUTO_INCREMENT — EF Core inserts without specifying Id; DB generates it.
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            // Timestamp is set by the C# model (DateTime.UtcNow) — no DB-level default needed.
            // Pomelo's generated DEFAULT UTC_TIMESTAMP() syntax is invalid on MySQL 8.0,
            // so we skip HasDefaultValueSql here and let the application layer supply the value.

            // HasColumnName forces EF Core to use snake_case column names in MySQL.
            // Without this, EF defaults to PascalCase (TenantId, ActorKeyId, etc.)
            // which breaks all Dapper queries that expect snake_case column names.
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

            // Indexes — speed up the most common query patterns:
            // (tenant_id, timestamp) for filtered date-range queries per tenant
            // (actor_key_id) for "who did what" queries
            entity.HasIndex(e => new { e.TenantId, e.Timestamp })
                  .HasDatabaseName("idx_audit_tenant_time");
            entity.HasIndex(e => e.ActorKeyId)
                  .HasDatabaseName("idx_audit_actor");
        });
    }
}
