namespace ControlIT.Api.Tests.Unit;

using System.ComponentModel.DataAnnotations;
using ControlIT.Api.Domain.DTOs.Requests;
using Xunit;

[Trait("Category", "Unit")]
public class BatchCommandRequestTests
{
    [Fact]
    public void Validate_RejectsOversizedBatch()
    {
        var request = new BatchCommandRequest
        {
            DeviceIds = Enumerable.Range(1, BatchCommandRequest.MaxDeviceCount + 1).ToList(),
            Command = "whoami",
            Shell = "bash",
            TimeoutSeconds = 30
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("cannot exceed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsNullDeviceIds()
    {
        var request = new BatchCommandRequest
        {
            DeviceIds = null!,
            Command = "whoami",
            Shell = "bash",
            TimeoutSeconds = 30
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsDuplicateDeviceIds()
    {
        var request = new BatchCommandRequest
        {
            DeviceIds = [27, 27],
            Command = "whoami",
            Shell = "bash",
            TimeoutSeconds = 30
        };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    private static List<ValidationResult> Validate(BatchCommandRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
        return results;
    }
}
