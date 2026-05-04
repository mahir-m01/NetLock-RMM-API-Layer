namespace ControlIT.Api.Common.Configuration;

public class DatabaseOptions
{
    public string Name { get; set; } = "netlock";
    public int MaxPoolSize { get; set; } = 100;
    public int MinPoolSize { get; set; } = 5;
    public int ConnectionLifetimeSeconds { get; set; } = 300;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
}
