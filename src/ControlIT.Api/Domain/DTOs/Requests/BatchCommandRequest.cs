using System.ComponentModel.DataAnnotations;

namespace ControlIT.Api.Domain.DTOs.Requests;

public class BatchCommandRequest : IValidatableObject
{
    public const int MaxDeviceCount = 25;

    [Required]
    public List<int> DeviceIds { get; set; } = [];

    [Required, StringLength(10000)]
    public string Command { get; set; } = string.Empty;

    [Required, RegularExpression("^(cmd|powershell|bash)$")]
    public string Shell { get; set; } = "cmd";

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeviceIds is null)
        {
            yield return new ValidationResult(
                "Device ids are required.",
                [nameof(DeviceIds)]);
            yield break;
        }

        if (DeviceIds.Count is 0)
        {
            yield return new ValidationResult(
                "At least one device id is required.",
                [nameof(DeviceIds)]);
        }

        if (DeviceIds.Count > MaxDeviceCount)
        {
            yield return new ValidationResult(
                $"Batch size cannot exceed {MaxDeviceCount} devices.",
                [nameof(DeviceIds)]);
        }

        if (DeviceIds.Any(id => id <= 0))
        {
            yield return new ValidationResult(
                "Device ids must be positive integers.",
                [nameof(DeviceIds)]);
        }

        if (DeviceIds.Distinct().Count() != DeviceIds.Count)
        {
            yield return new ValidationResult(
                "Device ids must be unique.",
                [nameof(DeviceIds)]);
        }
    }
}
