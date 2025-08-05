using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Sqs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pager.Duty.Requests;
using Xunit;
using Environment = Defra.Cdp.Notify.Backend.Api.Models.Environment;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services;

public class GrafanaEventEndToEndTest(MongoIntegrationTest fixture) : EndToEndTestBase(fixture)
{
    [Fact]
    public async Task FailingWorkflowRunTriggerPagerDutyAlert()
    {
        var config = Substitute.For<IOptions<GrafanaAlertListenerConfig>>();
        const string sqsQueueUrl = "https://fake-sqs-url";
        config.Value.Returns(new GrafanaAlertListenerConfig { QueueUrl = sqsQueueUrl, Enabled = true });

        var listener = new GrafanaAlertListener(Sqs, config,
            new GrafanaAlertHandler(NotifyHandler, SqsMessageService,
                new Logger<GrafanaAlertHandler>(LoggerFactory)),
            new Logger<GrafanaAlertListener>(LoggerFactory));

        var messages = new List<Message>
        {
            new()
            {
                MessageId = "12345",
                Body = """
                       {
                         "environment": "prod",
                         "team": "Platform",
                         "service": "test-repository",
                         "pagerDuty": "true",
                         "alertName": "pager-duty-alert",
                         "status": "firing",
                         "startsAt": "2025-07-11T11:57:50Z",
                         "endsAt": "0001-01-01T00:00:00Z",
                         "summary": "Will fire because status is firing",
                         "description": "Valid status",
                         "series": "",
                         "runbookUrl": "",
                         "alertURL": "https://metrics.prod.cdp-int.defra.cloud/alerting/grafana/ae0ko9ppsqv40e/view"
                       }
                       """
            }
        };

        var withMessages = Task.FromResult(new ReceiveMessageResponse { Messages = messages });

        Sqs.ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>()).Returns(
            withMessages,
            Task.FromException<ReceiveMessageResponse>(new OperationCanceledException())
        );

        await RulesService.PersistRule(new AlertRule(Source.Grafana, Environment.Prod, null, true,
            [AlertMethod.Slack]), CancellationToken.None);

        await RulesService.PersistRule(new AlertRule(Source.Grafana, Environment.Prod, "test-repository", true,
            [AlertMethod.PagerDuty]), CancellationToken.None);

        await EntitiesService.PersistAll(
            [new Entity("test-repository", [new PortalBackendTeam("team-1234", "TestTeam")])], CancellationToken.None);

        await TeamsService.PersistAll(["TestTeam"], CancellationToken.None);

        SecretsService.PagerDutyIntegrationKey("TestTeam", CancellationToken.None).Returns("test-team-integration-key");


        await listener.ReadAsync(CancellationToken.None);


        var storedMessage = await SqsMessageService.GetMessage(Source.Grafana, "12345", CancellationToken.None);
        Assert.Equal("12345", storedMessage.MessageId);

        await SecretsService.Received(1).PagerDutyIntegrationKey("TestTeam", CancellationToken.None);

        var storedAlertNotification =
            await AlertNotificationService.GetNotificationByAwsMessageId("12345", CancellationToken.None);
        Assert.Equal("12345", storedAlertNotification.NotifyEvent.AwsMessageId);
        Assert.Equal(AlertMethod.PagerDuty, storedAlertNotification.AlertRule.Methods[0]);

        await Sqs.Received().ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>());

        await PagerDutyClient.Received(1).SendAlert("test-team-integration-key",
            Arg.Is<TriggerAlert>(a =>
                a.Severity == Severity.Critical 
                && a.Summary == "Will fire because status is firing"
                && a.Source == nameof(Source.Grafana)), CancellationToken.None);
    }
}