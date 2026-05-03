using System.ComponentModel.DataAnnotations;

namespace ControlIT.Api.Domain.DTOs.Requests;

public class CommandRequest
{
    [Range(1, int.MaxValue)]
    public int DeviceId { get; set; }

    [Required, StringLength(10000)]
    public string Command { get; set; } = string.Empty;

    [Required, RegularExpression("^(cmd|powershell|bash)$")]
    public string Shell { get; set; } = "cmd";

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
