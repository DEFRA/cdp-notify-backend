using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sns;

public interface ISnsPublisher
{
    Task Publish(string topicArn, string message, string environment);
}

public class SnsPublisher(IAmazonSimpleNotificationService snsClient) : ISnsPublisher
{
    public async Task Publish(string topicArn, string message, string environment)
    {
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

        var response = await snsClient.PublishAsync(request);

        Console.WriteLine($"MessageId: {response.MessageId}");
        }
    }