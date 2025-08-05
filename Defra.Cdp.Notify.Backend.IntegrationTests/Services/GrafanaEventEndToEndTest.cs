using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Email;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;
using Defra.Cdp.Notify.Backend.Api.Services.Secrets;
using Defra.Cdp.Notify.Backend.Api.Services.Slack;
using Defra.Cdp.Notify.Backend.Api.Services.Sns;
using Defra.Cdp.Notify.Backend.Api.Services.Sqs;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using Defra.Cdp.Notify.Backend.IntegrationTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pager.Duty.Requests;
using Xunit;
using Environment = Defra.Cdp.Notify.Backend.Api.Models.Environment;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services;

public class GrafanaEventEndToEndTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task FailingWorkflowRunTriggerPagerDutyAlert()
    {
        var mongoConfig = Substitute.For<IOptions<MongoConfig>>();
        mongoConfig.Value.Returns(new MongoConfig
        {
            DatabaseName = "GrafanaEventEndToEndTest", DatabaseUri = Fixture.ConnectionString
        });
        var mongoFactory = new MongoDbClientFactory(mongoConfig);

        var sqs = Substitute.For<IAmazonSQS>();
        var config = Substitute.For<IOptions<GrafanaAlertListenerConfig>>();
        const string sqsQueueUrl = "https://fake-sqs-url";
        config.Value.Returns(new GrafanaAlertListenerConfig { QueueUrl = sqsQueueUrl, Enabled = true });
        var loggerFactory = Substitute.For<ILoggerFactory>();

        var alertNotificationService = new AlertNotificationService(mongoFactory, loggerFactory);
        var teamOverridesService = new TeamOverridesService(mongoFactory, loggerFactory);
        var entitiesService = new EntitiesService(mongoFactory, loggerFactory);

        var secretsService = Substitute.For<ISecretsService>();
        var pagerDutyClient = Substitute.For<IPagerDutyClient>();
        var pagerDutyAlertHandler = new PagerDutyAlertHandler(
            pagerDutyClient,
            alertNotificationService,
            entitiesService,
            secretsService,
            teamOverridesService,
            new PagerDutyAlertBuilder(),
            new Logger<PagerDutyAlertHandler>(loggerFactory));

        var teamsService = new TeamsService(mongoFactory, loggerFactory);

        var emailAlertHandler = new EmailAlertHandler(alertNotificationService, teamOverridesService, entitiesService,
            teamsService,
            new EmailBuilder(),
            Substitute.For<IEmailClient>(),
            new Logger<EmailAlertHandler>(loggerFactory)
        );

        var snsPublisher = Substitute.For<ISnsPublisher>();
        var slackHandlerConfig = Substitute.For<IOptions<SlackHandlerConfig>>();
        const string testSlackArn = "test-slack-arn";
        slackHandlerConfig.Value.Returns(new SlackHandlerConfig { TopicArn = testSlackArn });
        var slackAlertHandler = new SlackAlertHandler(alertNotificationService, teamOverridesService, entitiesService,
            teamsService,
            snsPublisher,
            slackHandlerConfig,
            new Logger<SlackAlertHandler>(loggerFactory));

        var rulesService = new RulesService(mongoFactory, loggerFactory);
        var notifyHandler = new NotifyEventHandler(
            rulesService,
            pagerDutyAlertHandler,
            emailAlertHandler,
            slackAlertHandler,
            new Logger<NotifyEventHandler>(loggerFactory)
        );

        var sqsMessageService = new SqsMessageService(mongoFactory, loggerFactory);
        var listener = new GrafanaAlertListener(sqs, config,
            new GrafanaAlertHandler(notifyHandler, sqsMessageService,
                new Logger<GrafanaAlertHandler>(loggerFactory)),
            new Logger<GrafanaAlertListener>(loggerFactory));

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

        sqs.ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>()).Returns(
            withMessages,
            Task.FromException<ReceiveMessageResponse>(new OperationCanceledException())
        );

        await rulesService.PersistRule(new AlertRule(Source.Grafana, Environment.Prod, null, true,
            [AlertMethod.Slack]), CancellationToken.None);

        await rulesService.PersistRule(new AlertRule(Source.Grafana, Environment.Prod, "test-repository", true,
            [AlertMethod.PagerDuty]), CancellationToken.None);

        await entitiesService.PersistAll(
            [new Entity("test-repository", [new PortalBackendTeam("team-1234", "TestTeam")])], CancellationToken.None);

        await teamsService.PersistAll(["TestTeam"], CancellationToken.None);

        secretsService.PagerDutyIntegrationKey("TestTeam", CancellationToken.None).Returns("test-team-integration-key");


        await listener.ReadAsync(CancellationToken.None);


        var storedMessage = await sqsMessageService.GetMessage(Source.Grafana, "12345", CancellationToken.None);
        Assert.Equal("12345", storedMessage.MessageId);

        await secretsService.Received(1).PagerDutyIntegrationKey("TestTeam", CancellationToken.None);

        var storedAlertNotification =
            await alertNotificationService.GetNotificationByAwsMessageId("12345", CancellationToken.None);
        Assert.Equal("12345", storedAlertNotification.NotifyEvent.AwsMessageId);
        Assert.Equal(AlertMethod.PagerDuty, storedAlertNotification.AlertRule.Methods[0]);

        await sqs.Received().ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>());

        await pagerDutyClient.Received(1).SendAlert("test-team-integration-key",
            Arg.Is<TriggerAlert>(a =>
                a.Severity == Severity.Critical 
                && a.Summary == "Will fire because status is firing"
                && a.Source == nameof(Source.Grafana)));

        listener.Dispose();
    }
}