namespace ControlIT.Api.Domain.Models;

public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? ReplacedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}
