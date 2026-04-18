namespace ControlIT.Api.Domain.DTOs.Requests;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
