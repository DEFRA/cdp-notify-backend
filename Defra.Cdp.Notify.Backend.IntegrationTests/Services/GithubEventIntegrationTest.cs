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
using Xunit;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services;

public class GithubEventIntegrationTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task FailingWorkflowRunTriggerSlackMessage()
    {
        var mongoConfig = Substitute.For<IOptions<MongoConfig>>();
        mongoConfig.Value.Returns(new MongoConfig
        {
            DatabaseName = "GithubEventIntegrationTest", DatabaseUri = Fixture.ConnectionString
        });
        var mongoFactory = new MongoDbClientFactory(mongoConfig);

        var sqs = Substitute.For<IAmazonSQS>();
        var config = Substitute.For<IOptions<GithubEventListenerConfig>>();
        const string sqsQueueUrl = "https://fake-sqs-url";
        config.Value.Returns(new GithubEventListenerConfig { QueueUrl = sqsQueueUrl, Enabled = true });
        var loggerFactory = Substitute.For<ILoggerFactory>();

        var alertNotificationService = new AlertNotificationService(mongoFactory, loggerFactory);
        var teamOverridesService = new TeamOverridesService(mongoFactory, loggerFactory);
        var entitiesService = new EntitiesService(mongoFactory, loggerFactory);

        var pagerDutyAlertHandler = new PagerDutyAlertHandler(
            Substitute.For<IPagerDutyClient>(),
            alertNotificationService,
            entitiesService,
            Substitute.For<ISecretsService>(),
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
        slackHandlerConfig.Value.Returns(new SlackHandlerConfig
        {
            TopicArn = testSlackArn
        });
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
        var listener = new GithubEventListener(sqs, config,
            new GithubEventHandler(notifyHandler, sqsMessageService,
                new Logger<GithubEventHandler>(loggerFactory)),
            new Logger<GithubEventListener>(loggerFactory));

        var messages = new List<Message>
        {
            new()
            {
                MessageId = "1",
                Body = """
                    {
                      "github_event": "workflow_run",
                      "action": "completed",
                      "repository": {
                        "name": "test-repository",
                        "html_url": "https://github.com/defra/test-repository"
                      },
                      "workflow_run": {
                        "name": "Build and Test",
                        "html_url": "https://github.com/defra/test-repository/actions/runs/123456789",
                        "run_number": 42,
                        "head_branch": "main",
                        "head_commit": {
                          "message": "Fix bug in SQS listener",
                          "author": {
                            "name": "Phil Segal"
                          }
                        },
                        "conclusion": "failure"
                      }
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

        await rulesService.PersistRule(new AlertRule(Source.Github, null, "test-repository", null,
            [AlertMethod.Slack]), CancellationToken.None);

        await entitiesService.PersistAll(
            [new Entity("test-repository", [new PortalBackendTeam("team-1234", "TestTeam")])], CancellationToken.None);
        
        await teamsService.PersistAll(["TestTeam"], CancellationToken.None);

        await teamsService.UpdateTeam("TestTeam", "test-slack-channel", [], CancellationToken.None);
        
        snsPublisher.Publish(testSlackArn, Arg.Is<string>(s => s.Contains("test-slack-channel")), "local").Returns(Task.CompletedTask);
        
        
        
        
        await listener.ReadAsync(CancellationToken.None);
        
        

        var storedMessage = await sqsMessageService.GetMessage(Source.Github, "1", CancellationToken.None);
        Assert.Equal("1", storedMessage.MessageId);
        
        var storedAlertNotification = await alertNotificationService.GetNotificationByAwsMessageId("1", CancellationToken.None);
        Assert.Equal("1", storedAlertNotification.NotifyEvent.AwsMessageId);
        Assert.Equal(AlertMethod.Slack, storedAlertNotification.AlertRule.Methods[0]);

        sqs.Received().ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>());
        snsPublisher.Received(1)
            .Publish(testSlackArn, Arg.Is<string>(s => s.Contains("test-slack-channel")), "local");
        
        
        listener.Dispose();
    }
}