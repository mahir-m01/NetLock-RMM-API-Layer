namespace ControlIT.Api.Domain.DTOs.Responses;

public class BatchCommandResponse
{
    public int RequestedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public IReadOnlyList<BatchCommandDeviceResult> Results { get; set; } = [];
}

public class BatchCommandDeviceResult
{
    public int DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
}
