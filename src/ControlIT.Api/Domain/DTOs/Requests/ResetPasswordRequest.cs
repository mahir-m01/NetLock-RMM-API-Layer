namespace ControlIT.Api.Domain.DTOs.Requests;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
