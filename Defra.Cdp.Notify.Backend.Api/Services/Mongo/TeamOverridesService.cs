using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Services.Mongo;

public interface ITeamOverridesService
{
    Task<TeamOverride?> GetOverrideForService(string serviceName, CancellationToken cancellationToken);

    Task<List<TeamOverride>> GetOverrides();

    Task<bool> Persist(TeamOverride teamOverride, CancellationToken cancellationToken);
}

public class TeamOverridesService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<TeamOverride>(connectionFactory, CollectionName, loggerFactory), ITeamOverridesService
{
    private const string CollectionName = "teamoverrides";

    protected override List<CreateIndexModel<TeamOverride>> DefineIndexes(
        IndexKeysDefinitionBuilder<TeamOverride> builder)
    {
        return
        [
            new CreateIndexModel<TeamOverride>(builder.Ascending(t => t.Service),
                new CreateIndexOptions { Unique = true })
        ];
    }

    public async Task<List<TeamOverride>> GetOverrides()
    {
        return await Collection.Find(Builders<TeamOverride>.Filter.Empty).ToListAsync();
    }

    public async Task<TeamOverride?> GetOverrideForService(string serviceName, CancellationToken cancellationToken)
    {
        var filter = Builders<TeamOverride>.Filter.Eq(x => x.Service, serviceName);

        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> Persist(TeamOverride teamOverride, CancellationToken cancellationToken)
    {
        try
        {
            await Collection.InsertOneAsync(teamOverride, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to insert {TeamOverride}", teamOverride);
            return false;
        }
    }
}