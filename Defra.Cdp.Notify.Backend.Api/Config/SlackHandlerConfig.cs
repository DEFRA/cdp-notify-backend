namespace Defra.Cdp.Notify.Backend.Api.Config;

public class SlackHandlerConfig
{
    public const string ConfigKey = "SlackHandler";
    public string TopicArn { get; init; } = default!;
}