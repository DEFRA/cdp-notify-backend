using System.Text.Json.Serialization;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Services.Mongo;

public interface ITeamsService
{
    Task<Team> GetTeam(string teamName, CancellationToken cancellationToken);
    Task<List<Team>> GetAllTeams(CancellationToken cancellationToken);
    Task PersistAll(HashSet<string> allTeamNames, CancellationToken cancellationToken);
    Task<List<Team>> GetTeams(List<string>? teamNames, CancellationToken cancellationToken);

    Task UpdateTeam(string teamName, string slackChannel, List<string> alertEmailAddresses,
        CancellationToken cancellationToken);
}

public class TeamsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<Team>(connectionFactory, CollectionName, loggerFactory), ITeamsService
{
    private const string CollectionName = "teams";

    protected override List<CreateIndexModel<Team>> DefineIndexes(IndexKeysDefinitionBuilder<Team> builder)
    {
        return
        [
            new CreateIndexModel<Team>(builder.Ascending(x => x.Name), new CreateIndexOptions { Unique = true })
        ];
    }

    public async Task<List<Team>> GetAllTeams(CancellationToken cancellationToken)
    {
        return await Collection.Find(Builders<Team>.Filter.Empty).ToListAsync(cancellationToken);
    }

    public async Task<Team> GetTeam(string teamName, CancellationToken cancellationToken)
    {
        var filter = Builders<Team>.Filter.Eq(e => e.Name, teamName);
        var result = await Collection.Find(filter).FirstAsync(cancellationToken);
        if (result is null)
            throw new KeyNotFoundException($"Team with teamName '{teamName}' not found.");
        return result;
    }

    public async Task PersistAll(HashSet<string> allTeamNames, CancellationToken cancellationToken)
    {
        var existingTeamNames = await Collection
            .Find(FilterDefinition<Team>.Empty)
            .Project(t => t.Name)
            .ToListAsync(cancellationToken);

        var newTeamNames = allTeamNames.Except(existingTeamNames);

        var newTeams = newTeamNames.Select(name =>
            new Team(name, [], null)
        ).ToList();

        if (newTeams.Count != 0)
        {
            await Collection.InsertManyAsync(newTeams, cancellationToken: cancellationToken);
        }

        var orphanedTeamNames = existingTeamNames.Except(allTeamNames).ToList();

        if (orphanedTeamNames.Count != 0)
        {
            var deleteFilter = Builders<Team>.Filter.In(t => t.Name, orphanedTeamNames);
            await Collection.DeleteManyAsync(deleteFilter, cancellationToken);
        }
    }

    public async Task<List<Team>> GetTeams(List<string>? teamNames, CancellationToken cancellationToken)
    {
        var filter = Builders<Team>.Filter.In(t => t.Name, teamNames);
        if (teamNames == null || teamNames.Count == 0)
        {
            filter = Builders<Team>.Filter.Empty;
        }

        return await Collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task UpdateTeam(string teamName, string slackChannel, List<string> alertEmailAddresses,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Team>.Filter.Eq(t => t.Name, teamName);
        var update = Builders<Team>.Update
            .Set(t => t.SlackChannel, slackChannel)
            .Set(t => t.AlertEmailAddresses, alertEmailAddresses);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}

[BsonIgnoreExtraElements]
public record Team(
    [property: JsonPropertyName("teamName")]
    string Name,
    [property: JsonPropertyName("alertEmailAddresses")]
    List<string> AlertEmailAddresses,
    [property: JsonPropertyName("slackChannel")]
    string? SlackChannel
);