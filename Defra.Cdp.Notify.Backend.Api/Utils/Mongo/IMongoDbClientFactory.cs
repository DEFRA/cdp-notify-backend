using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Utils.Mongo;

public interface IMongoDbClientFactory
{
    IMongoClient GetClient();

    IMongoCollection<T> GetCollection<T>(string collection);
}