using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Services.Mongo;

public interface ISqsMessageService
{
    Task SaveMessage(Source source, Message message, CancellationToken cancellationToken);
}

public class SqsMessageService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<SqsMessage>(connectionFactory, CollectionName, loggerFactory), ISqsMessageService
{
    private const string CollectionName = "sqsmessages";
    protected override List<CreateIndexModel<SqsMessage>> DefineIndexes(IndexKeysDefinitionBuilder<SqsMessage> builder)
    {
        var indexModel = new CreateIndexModel<SqsMessage>(
            builder.Ascending(x => x.Source).Ascending(x => x.DateTime)
        );

        return [indexModel];
    }

    public async Task SaveMessage(Source source, Message message, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(new SqsMessage(source, message.MessageId, DateTime.Now, message.Body),
            cancellationToken: cancellationToken);
    }
}