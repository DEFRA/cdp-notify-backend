using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;

namespace Defra.Cdp.Notify.Backend.Api.Services.Email;

public interface IEmailAlertHandler : IAlertHandler;

public class EmailAlertHandler(
    IAlertNotificationService alertNotificationService,
    ITeamOverridesService teamOverridesService,
    IEntitiesService entitiesService,
    ITeamsService teamsService,
    IEmailBuilder emailBuilder,
    IEmailClient emailClient,
    ILogger<EmailAlertHandler> logger
) : AlertHandler(alertNotificationService, teamOverridesService, entitiesService), IEmailAlertHandler
{
    protected override async Task HandleInternal(AlertNotification alertNotification, List<string> teamNames,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PagerDuty alert for AlertNotification {EventId}", alertNotification.Id);


        var teams = await teamsService.GetTeams(teamNames, cancellationToken);
        var emailAddresses = teams
            .SelectMany(t => t.AlertEmailAddresses)
            .ToHashSet();

        var emailContent = emailBuilder.BuildEmail(alertNotification, emailAddresses);

        emailClient.SendEmail(emailContent, cancellationToken);
    }
}