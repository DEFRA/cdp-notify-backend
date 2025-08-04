namespace Defra.Cdp.Notify.Backend.Api.Config;

public class PagerDutyConfig
{
    public const string ConfigKey = "PagerDuty";
    public required string Url { get; init; }
}