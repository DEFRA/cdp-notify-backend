using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Services.Mongo;

public interface IEntitiesService
{
    Task<Entity> GetEntity(string name, CancellationToken cancellationToken);

    Task PersistAll(List<Entity> entities, CancellationToken cancellationToken);
}

public class EntitiesService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<Entity>(connectionFactory, CollectionName, loggerFactory), IEntitiesService
{
    private const string CollectionName = "entities";

    protected override List<CreateIndexModel<Entity>> DefineIndexes(IndexKeysDefinitionBuilder<Entity> builder)
    {
        return [];
    }

    public async Task<List<Entity>> GetEntities()
    {
        return await Collection.Find(Builders<Entity>.Filter.Empty).ToListAsync();
    }

    public async Task<Entity> GetEntity(string name, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(e => e.Name, name);
        var result = await Collection.Find(filter).FirstAsync(cancellationToken);
        if (result is null)
            throw new KeyNotFoundException($"Entity with name '{name}' not found.");
        return result;
    }

    public async Task PersistAll(List<Entity> entities, CancellationToken cancellationToken)
    {
        var writes = entities.Select(entity =>
            new ReplaceOneModel<Entity>(
                Builders<Entity>.Filter.Eq(e => e.Name, entity.Name),
                entity)
            {
                IsUpsert = true
            }).ToList();

        await Collection.BulkWriteAsync(writes, cancellationToken: cancellationToken);
        
        var names = entities.Select(e => e.Name).ToList();
        var deleteFilter = Builders<Entity>.Filter.Nin(e => e.Name, names);
        await Collection.DeleteManyAsync(deleteFilter, cancellationToken);
    }
}