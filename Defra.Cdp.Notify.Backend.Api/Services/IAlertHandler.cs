using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;

namespace Defra.Cdp.Notify.Backend.Api.Services;

public interface IAlertHandler
{
    Task Handle(NotifyEvent notifyEvent, AlertRule alertRule, CancellationToken cancellationToken);
}

public abstract class AlertHandler(
    IAlertNotificationService alertNotificationService,
    ITeamOverridesService teamOverridesService,
    IEntitiesService entitiesService
) : IAlertHandler
{
    public async Task Handle(NotifyEvent notifyEvent, AlertRule alertRule, CancellationToken cancellationToken)
    {
        var alertNotification = new AlertNotification(
            notifyEvent,
            alertRule,
            DateTime.Now
        );
        await alertNotificationService.SaveNotification(alertNotification, cancellationToken);

        var teamOverride =
            await teamOverridesService.GetOverrideForService(alertNotification.NotifyEvent.Service, cancellationToken);

        var teams = teamOverride != null
            ? teamOverride.Teams
            : await TeamsForService(alertNotification.NotifyEvent.Service, cancellationToken);

        await HandleInternal(alertNotification, teams, cancellationToken);
    }


    private async Task<List<string>> TeamsForService(string serviceName, CancellationToken cancellationToken)
    {
        var service = await entitiesService.GetEntity(serviceName, cancellationToken);
        return service.Teams.Select(t => t.Name).ToList();
    }

    protected abstract Task HandleInternal(AlertNotification alertNotification, List<string> teamNames,
        CancellationToken cancellationToken);
}