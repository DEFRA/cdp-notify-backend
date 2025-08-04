namespace Defra.Cdp.Notify.Backend.Api.Config;

public class MongoConfig
{
    public const string ConfigKey = "Mongo";
    public string DatabaseUri { get; init; } = default!;
    public string DatabaseName { get; init; } = default!;
}