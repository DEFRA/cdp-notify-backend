using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Services.Mongo;

public interface IAlertNotificationService
{
    Task SaveNotification(AlertNotification notification, CancellationToken cancellationToken);
};

public class AlertNotificationService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AlertNotification>(connectionFactory, CollectionName, loggerFactory), IAlertNotificationService
{

    private const string CollectionName = "alertnotifications";

    protected override List<CreateIndexModel<AlertNotification>> DefineIndexes(
        IndexKeysDefinitionBuilder<AlertNotification> builder)
    {
        return [];
    }

    public async Task SaveNotification(AlertNotification notification, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(notification, cancellationToken: cancellationToken);
    }
    
    public async Task<AlertNotification> GetNotificationByAwsMessageId(string id, CancellationToken cancellationToken)
    {
        var filter = Builders<AlertNotification>.Filter.Eq(n => n.NotifyEvent.AwsMessageId, id);
        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

}