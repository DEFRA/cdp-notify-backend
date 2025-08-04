namespace Defra.Cdp.Notify.Backend.Api.Config;

public class GrafanaAlertListenerConfig
{
    public const string ConfigKey = "GrafanaAlertListener";
    public required string QueueUrl { get; init; }
    public required bool Enabled { get; init; }
}