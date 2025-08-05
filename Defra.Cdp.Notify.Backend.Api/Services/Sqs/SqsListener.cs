using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sqs;

public interface ISqsListener
{
    public Task ReadAsync(CancellationToken cancellationToken);
}

public abstract class SqsListener(IAmazonSQS sqs, ISqsMessageHandler messageHandler, string queueUrl, ILogger logger, bool enabled = true)
    : ISqsListener, IDisposable
{
    private bool _enabled = enabled;
    private const int WaitTimeoutSeconds = 15;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _enabled = false;
            sqs.Dispose();
        }
    }

    public async Task ReadAsync(CancellationToken cancellationToken)
    {
        #pragma warning disable CS0618 // Type or member is obsolete        
        var receiveMessageRequest = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeoutSeconds, AttributeNames = ["All"]
        };
        #pragma warning restore CS0618 // Type or member is obsolete
        
        logger.LogInformation("Listening for events on {Queue}", queueUrl);

        var falloff = 1;
        while (_enabled)
            try
            {
                var receiveMessageResponse = await sqs.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);

                if (receiveMessageResponse.Messages == null || receiveMessageResponse.Messages.Count == 0) continue;

                foreach (var message in receiveMessageResponse.Messages)
                {
                    await HandleMessage(message, cancellationToken);

                    await DeleteMessage(message, cancellationToken);
                }

                falloff = 1;
            }
            
            catch (OperationCanceledException e)
            {
                logger.LogInformation(e, "SQS listener cancelled/stopped for {Queue}.", queueUrl);
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error reading from SQS queue {Queue}", queueUrl);
                falloff++;
                if (falloff <= 10) continue;
                logger.LogCritical("Failed to read from SQS queue {Queue} after multiple attempts", queueUrl);
                throw new IOException($"Failed to read from SQS queue {queueUrl} after multiple attempts", e);
            }
    }

    private async Task DeleteMessage(Message message, CancellationToken cancellationToken)
    {
        var deleteRequest = new DeleteMessageRequest
        {
            QueueUrl = queueUrl, ReceiptHandle = message.ReceiptHandle
        };
        await sqs.DeleteMessageAsync(deleteRequest, cancellationToken);
    }

    private async Task HandleMessage(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await messageHandler.HandleMessage(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Message: {Id} - Exception: {Message}", message.MessageId, exception.Message);
        }
    }
}