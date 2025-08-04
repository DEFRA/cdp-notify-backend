using Amazon.SecretsManager;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using LocalStack.Client.Extensions;

namespace Defra.Cdp.Notify.Backend.Api.Utils;

public static class AwsClients
{

    public static void AddAwsClients(this IServiceCollection service, IConfiguration configuration, bool isDevMode)
    {
        if (isDevMode)
        {
            service.AddLocalStack(configuration);
            service.AddDefaultAWSOptions(configuration.GetAWSOptions());
            service.AddAwsService<IAmazonSQS>();
            service.AddAwsService<IAmazonSecretsManager>();
            service.AddAwsService<IAmazonSimpleNotificationService>();
        }
        else
        {
            var sqsClient = new AmazonSQSClient();
            var secretsClient = new AmazonSecretsManagerClient();
            var snsClient = new AmazonSimpleNotificationServiceClient();
            service.AddSingleton<IAmazonSQS>(sqsClient);
            service.AddSingleton<IAmazonSecretsManager>(secretsClient);
            service.AddSingleton<IAmazonSimpleNotificationService>(snsClient);
        }
    }
}