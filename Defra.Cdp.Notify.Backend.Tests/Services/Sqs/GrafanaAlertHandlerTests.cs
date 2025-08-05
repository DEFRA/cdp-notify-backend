using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.Sqs;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.Cdp.Notify.Backend.Tests.Services.Sqs;

public class GrafanaAlertHandlerTests
{
    [Fact]
    public async Task HandleMessageAsync_SavesMessageAndHandlesAlert()
    {
        var sqsMessageService = Substitute.For<ISqsMessageService>();
        var notifyEventHandler = Substitute.For<INotifyEventHandler>();
        var logger = Substitute.For<ILogger<GrafanaAlertHandler>>();

        var message = new Message
        {
            MessageId = "msg-1",
            Body = """
                   {
                    "service": "my-service",
                    "environment": "ext-test",
                    "alertUrl": "some-url",
                    "status": "firing",
                    "summary": "Test alert",
                    "alertURL": "https://example.com/alert/12345",
                    "startsAt": "2023-10-01T12:00:00Z"
                   }
                   """
        };

        var handler = new GrafanaAlertHandler(
            notifyEventHandler,
            sqsMessageService,
            logger
        );

        await handler.HandleMessage(message, CancellationToken.None);

        await sqsMessageService.Received(1).SaveMessage(Source.Grafana, message, Arg.Any<CancellationToken>());
        await notifyEventHandler.Received(1)
            .Handle(Arg.Is<NotifyEvent>(a => a.Source == Source.Grafana && a.Status == "firing"),
                Arg.Any<CancellationToken>());
    }
}