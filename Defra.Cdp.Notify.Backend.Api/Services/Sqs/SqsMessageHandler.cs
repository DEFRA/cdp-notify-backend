using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Utils;
using Environment = Defra.Cdp.Notify.Backend.Api.Models.Environment;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sqs;

public interface ISqsMessageHandler
{
    Task HandleMessage(Message message, CancellationToken cancellationToken);
}

public abstract class SqsMessageHandler<TType>(
    Source source,
    ISqsMessageService sqsMessageService,
    INotifyEventHandler notifyEventHandler,
    ILogger logger) : ISqsMessageHandler
    where TType : IAlert
{
    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new EnumMemberJsonConverter<Environment>() }
    };

    public async Task HandleMessage(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received {Source} message: {MessageId}", source, message.MessageId);

        try
        {
            await sqsMessageService.SaveMessage(source, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save {Source} message ${messageId} - {Error}:\n {body}",
                source, message.MessageId,
                ex.Message, message.Body);
        }

        try
        {
            var alert = JsonSerializer.Deserialize<TType>(message.Body, _options) ??
                        throw new JsonException("Deserialization resulted in null");

            var notifyEvent = AlertToNotifyEvent(alert, message);

            if (notifyEvent != null)
            {
                await notifyEventHandler.Handle(notifyEvent, cancellationToken);
            }
        }
        catch (JsonException ex)
        {
            logger.LogError("Failed to deserialize message {MessageId} - {Error}:\n {body}",
                message.MessageId, ex.Message, message.Body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle message {MessageId}, {Error}", message.MessageId, ex.Message);
        }
    }

    protected abstract NotifyEvent? AlertToNotifyEvent(TType alert, Message message);
}