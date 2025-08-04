using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Notify.Backend.Api.Config;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Notify.Backend.Api.Clients;

public interface IPortalBackendClient
{
    Task<List<Entity>> GetEntities(CancellationToken cancellationToken);
}

public class PortalBackendClient : IPortalBackendClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public PortalBackendClient(IOptions<PortalBackendConfig> config, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = config.Value.Url;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new ArgumentException("Portal backend url cannot be null", nameof(config));
        _client = httpClientFactory.CreateClient("PortalBackendClient");
    }

    public async Task<List<Entity>> GetEntities(CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl + $"/entities?status=Created", cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<Entity>>(response, cancellationToken: cancellationToken) ?? throw new InvalidOperationException();
    }
}


[BsonIgnoreExtraElements]
public record  Entity(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("teams")] PortalBackendTeam[] Teams
);
public record PortalBackendTeam(
    [property: JsonPropertyName("teamId")] string TeamId,
    [property: JsonPropertyName("name")] string Name
);
