using System.Text.Json;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.Sns;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Notify.Backend.Api.Services.Slack;

public interface ISlackAlertHandler : IAlertHandler;

public class SlackAlertHandler(
    IAlertNotificationService alertNotificationService,
    ITeamOverridesService teamOverridesService,
    IEntitiesService entitiesService,
    ITeamsService teamsService,
    ISnsPublisher snsPublisher,
    IOptions<SlackHandlerConfig> slackHandlerConfig,
    ILogger<SlackAlertHandler> logger)
    : AlertHandler(alertNotificationService, teamOverridesService, entitiesService), ISlackAlertHandler
{
    private readonly string _environment = System.Environment.GetEnvironmentVariable("service.environment") ?? "local";

    protected override async Task HandleInternal(AlertNotification alertNotification, List<string> teamNames,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling Slack alert for AlertNotification {EventId}", alertNotification.Id);

        foreach (var teamName in teamNames)
        {
            var team = await teamsService.GetTeam(teamName, cancellationToken);
            if (team.SlackChannel != null)
            {
                var message = alertNotification.NotifyEvent switch
                {
                    GithubNotifyEvent githubEvent => SlackMessageBuilder.BuildSlackMessage(team.SlackChannel,
                        githubEvent.WorkflowName, githubEvent.Service, githubEvent.WorkflowUrl,
                        githubEvent.RunNumber, githubEvent.CommitMessage, githubEvent.Author),
                    _ => throw new ArgumentException("Unsupported notify event type: " + nameof(alertNotification.NotifyEvent))
                };
                await snsPublisher.Publish(slackHandlerConfig.Value.TopicArn, JsonSerializer.Serialize(message),
                    _environment);
            }
            else
            {
                logger.LogInformation("No Slack channel found for {EventId} for team: {Team}", alertNotification.NotifyEvent, team);
            }
        }
    }
}