using Microsoft.Graph;
using Azure.Identity;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Services.Email;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Defra.Cdp.Notify.Backend.Api.Clients;

public interface IEmailClient
{
    void SendEmail(Email emailContent, CancellationToken cancellationToken);
}

public class EmailClient(
    IOptions<EmailClientConfig> mailClientConfig,
    IPortalBackendClient portalBackendClient,
    ILogger<EmailClient> logger)
    : IEmailClient
{
    public async void SendEmail(Email emailContent, CancellationToken cancellationToken)
    {
        if (await portalBackendClient.IsFeatureToggleActive("disable-notify-publish", cancellationToken))
        {
            logger.LogInformation("Feature toggle 'disable-notify-publish' is active, skipping sending email.");
            return;
        }

        try
        {
            var credential = new DefaultAzureCredential();
            logger.LogInformation("Sending email: {EmailContent}", emailContent);
            logger.LogInformation("Base Url: {Baseurl}", mailClientConfig.Value.BaseUrl);

            var graphClient = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"],
                mailClientConfig.Value.BaseUrl);

            var recipients = emailContent.ToAddresses.Select(address => new Recipient
            {
                EmailAddress = new EmailAddress { Address = address }
            }).ToList();

            var message = new Message
            {
                Subject = emailContent.Subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = emailContent.Body },
                ToRecipients = recipients
            };

            await graphClient.Users[mailClientConfig.Value.SenderAddress]
                .SendMail
                .PostAsync(
                    new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                    {
                        Message = message, SaveToSentItems = true
                    }, null, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending email: {Message}", e.Message);
        }
    }
}