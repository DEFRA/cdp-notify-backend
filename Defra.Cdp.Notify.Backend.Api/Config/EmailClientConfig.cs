namespace Defra.Cdp.Notify.Backend.Api.Config;

public class EmailClientConfig
{
    public const string ConfigKey = "EmailClient";
    public required string SenderAddress { get; init; }
    public string? BaseUrl { get; init; }
}