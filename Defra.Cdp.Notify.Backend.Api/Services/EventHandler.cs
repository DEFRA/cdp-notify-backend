using System.Text.Json.Serialization;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Email;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;
using Defra.Cdp.Notify.Backend.Api.Services.Slack;

namespace Defra.Cdp.Notify.Backend.Api.Services;

public interface INotifyEventHandler{
    public Task Handle(NotifyEvent notifyEvent, CancellationToken cancellationToken);
}

public class NotifyEventHandler(
    IRulesService rulesService,
    IPagerDutyAlertHandler pagerDutyAlertHandler,
    IEmailAlertHandler emailAlertHandler,
    ISlackAlertHandler slackAlertHandler,
    ILogger<NotifyEventHandler> logger
    ) : INotifyEventHandler
{
    public async Task Handle(NotifyEvent notifyEvent, CancellationToken cancellationToken)
    {
        var alertRule = notifyEvent switch
        {
            GrafanaNotifyEvent grafanaEvent => await rulesService.GetAlertRuleForGrafanaEvent(grafanaEvent, cancellationToken),
            GithubNotifyEvent githubEvent => await rulesService.GetAlertRuleForGithubEvent(githubEvent, cancellationToken),
            _ => throw new ArgumentException("Unsupported notify event type", nameof(notifyEvent))
        };

        if (alertRule == null)
        {
            logger.LogInformation("No Alert Rule found for event: {NotifyEvent}", notifyEvent);
            return;
        }

        logger.LogInformation("Alert Rule found: {AlertRule}", alertRule);

        foreach (var method in alertRule.Methods)
        {
            switch(method)
            {
                case AlertMethod.PagerDuty:
                    logger.LogInformation("Pager Duty Alert");
                    await pagerDutyAlertHandler.Handle(notifyEvent, alertRule, cancellationToken);
                    break;
                case AlertMethod.Email:
                    logger.LogInformation("Email Alert");
                    await emailAlertHandler.Handle(notifyEvent, alertRule, cancellationToken);
                    break;
                case AlertMethod.Slack:
                    logger.LogInformation("Slack Alert");
                    await slackAlertHandler.Handle(notifyEvent, alertRule, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Alert method {method} is not supported.");
            }
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertMethod
{
    PagerDuty,
    Email,
    Slack
}