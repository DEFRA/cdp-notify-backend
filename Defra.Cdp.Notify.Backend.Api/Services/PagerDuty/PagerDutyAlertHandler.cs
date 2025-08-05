using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.Secrets;
using Pager.Duty.Requests;

namespace Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;

public interface IPagerDutyAlertHandler : IAlertHandler;

public class PagerDutyAlertHandler(
    IPagerDutyClient pagerDutyClient,
    IAlertNotificationService alertNotificationService,
    IEntitiesService entitiesService,
    ISecretsService secretsService,
    ITeamOverridesService teamOverridesService,
    IPagerDutyAlertBuilder pagerDutyAlertBuilder,
    ILogger<PagerDutyAlertHandler> logger)
    : AlertHandler(alertNotificationService, teamOverridesService, entitiesService), IPagerDutyAlertHandler
{
    protected override async Task HandleInternal(AlertNotification alertNotification, List<string> teamNames,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling PagerDuty alert for AlertNotification {EventId}", alertNotification.Id);
        
        foreach (var team in teamNames)
        {
            var integrationKey = await secretsService.PagerDutyIntegrationKey(team, cancellationToken);
            if (integrationKey == null)
            {
                logger.LogInformation("No integration key for team {Team}, skipping PagerDuty alert", team);
                continue;
            }
            var alert = pagerDutyAlertBuilder.BuildPagerDutyAlert(alertNotification, teamNames, Severity.Critical);
            await pagerDutyClient.SendAlert(integrationKey, alert);
        }
    }
}