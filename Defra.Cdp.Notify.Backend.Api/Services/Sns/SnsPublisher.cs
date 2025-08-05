using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.Cdp.Notify.Backend.Api.Clients;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sns;

public interface ISnsPublisher
{
    Task Publish(string topicArn, string message, string environment, CancellationToken cancellationToken);
}

public class SnsPublisher(IAmazonSimpleNotificationService snsClient,
    IPortalBackendClient portalBackendClient,
    ILogger<SnsPublisher> logger) : ISnsPublisher
{
    public async Task Publish(string topicArn, string message, string environment, CancellationToken cancellationToken)
    {
        if (await portalBackendClient.IsFeatureToggleActive("disable-notify-publish", cancellationToken))
        {
            logger.LogInformation("Feature toggle 'disable-notify-publish' is active, skipping SNS publish.");
            return;
        }
        
        var request = new PublishRequest
        {
            TopicArn = topicArn,
            Message = message,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "Environment", new MessageAttributeValue { DataType = "String", StringValue = environment } }
            }
        };

        if (topicArn.EndsWith("fifo"))
        {
            request.MessageDeduplicationId = Guid.NewGuid().ToString();
            request.MessageGroupId = environment;
        }

        var response = await snsClient.PublishAsync(request, cancellationToken);

        Console.WriteLine($"MessageId: {response.MessageId}");
        }
    }