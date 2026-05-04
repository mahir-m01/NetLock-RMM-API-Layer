namespace ControlIT.Api.Common.Configuration;

public sealed class NetLockLiveBridgeOptions
{
    public int PollIntervalSeconds { get; set; } = 5;

    public int PageSize { get; set; } = 500;

    public TimeSpan PollInterval =>
        TimeSpan.FromSeconds(Math.Clamp(PollIntervalSeconds, 2, 60));

    public int ClampedPageSize => Math.Clamp(PageSize, 100, 1000);
}
