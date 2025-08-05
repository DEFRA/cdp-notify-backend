using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Sqs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services;

public class GithubEventEndToEndTest(MongoIntegrationTest fixture) : EndToEndTestBase(fixture)
{
    [Fact]
    public async Task FailingWorkflowRunTriggerSlackMessage()
    {
        const string sqsQueueUrl = "https://fake-sqs-url";
        var config = Substitute.For<IOptions<GithubEventListenerConfig>>();
        config.Value.Returns(new GithubEventListenerConfig { QueueUrl = sqsQueueUrl, Enabled = true });
        
        var listener = new GithubEventListener(Sqs, config,
            new GithubEventHandler(NotifyHandler, SqsMessageService,
                new Logger<GithubEventHandler>(LoggerFactory)),
            new Logger<GithubEventListener>(LoggerFactory));
        
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

        Sqs.ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>()).Returns(
            withMessages,
            Task.FromException<ReceiveMessageResponse>(new OperationCanceledException())
        );

        await RulesService.PersistRule(new AlertRule(Source.Github, null, "test-repository", null,
            [AlertMethod.Slack]), CancellationToken.None);

        await EntitiesService.PersistAll(
            [new Entity("test-repository", [new PortalBackendTeam("team-1234", "TestTeam")])], CancellationToken.None);
        
        await TeamsService.PersistAll(["TestTeam"], CancellationToken.None);

        await TeamsService.UpdateTeam("TestTeam", "test-slack-channel", [], CancellationToken.None);
        
        SnsPublisher.Publish(TestSlackArn, Arg.Is<string>(s => s.Contains("test-slack-channel")), "local", CancellationToken.None).Returns(Task.CompletedTask);
        
        await listener.ReadAsync(CancellationToken.None);
        

        var storedMessage = await SqsMessageService.GetMessage(Source.Github, "1", CancellationToken.None);
        Assert.Equal("1", storedMessage.MessageId);
        
        var storedAlertNotification = await AlertNotificationService.GetNotificationByAwsMessageId("1", CancellationToken.None);
        Assert.Equal("1", storedAlertNotification.NotifyEvent.AwsMessageId);
        Assert.Equal(AlertMethod.Slack, storedAlertNotification.AlertRule.Methods[0]);

        await Sqs.Received().ReceiveMessageAsync(Arg.Is<ReceiveMessageRequest>(r => r.QueueUrl == sqsQueueUrl),
            Arg.Any<CancellationToken>());
        await SnsPublisher.Received(1)
            .Publish(TestSlackArn, Arg.Is<string>(s => s.Contains("test-slack-channel")), "local", CancellationToken.None);
    }
}