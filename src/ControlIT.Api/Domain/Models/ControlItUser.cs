namespace ControlIT.Api.Domain.Models;

public sealed class ControlItUser
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required Role Role { get; set; }
    public int? TenantId { get; set; }
    public string? AssignedClientsJson { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime PasswordChangedAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public enum Role
{
    SuperAdmin = 0,
    CpAdmin = 1,
    ClientAdmin = 2,
    Technician = 3
}
