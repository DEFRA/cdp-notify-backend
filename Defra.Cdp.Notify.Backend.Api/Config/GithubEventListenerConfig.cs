namespace Defra.Cdp.Notify.Backend.Api.Config;

public class GithubEventListenerConfig
{
    public const string ConfigKey = "GithubEventListener";
    public required string QueueUrl { get; init; }
    public required bool Enabled { get; init; }
}