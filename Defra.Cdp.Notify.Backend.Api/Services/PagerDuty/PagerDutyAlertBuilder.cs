using System.Security.Cryptography;
using System.Text;
using Defra.Cdp.Notify.Backend.Api.Models;
using Pager.Duty.Requests;

namespace Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;

public interface IPagerDutyAlertBuilder
{
    Alert BuildPagerDutyAlert(AlertNotification alertNotification, List<string> teamNames, Severity severity);
}

public class PagerDutyAlertBuilder : IPagerDutyAlertBuilder
{
    public Alert BuildPagerDutyAlert(AlertNotification alertNotification, List<string> teamNames, Severity severity)
    {
        return alertNotification.NotifyEvent switch
        {
            GrafanaNotifyEvent => BuildGrafanaAlert(alertNotification, teamNames, severity),
            GithubNotifyEvent => BuildGithubAlert(alertNotification, teamNames, severity),
            _ => throw new ArgumentException("Unsupported NotifyEvent type {}", alertNotification.NotifyEvent.GetType().Name)
        };
    }

    private static Alert BuildGrafanaAlert(AlertNotification alertNotification, List<string> teams, Severity severity)
    {
        var notifyEvent = (GrafanaNotifyEvent)alertNotification.NotifyEvent;
        var dedupKey = DedupKey(notifyEvent);
        return notifyEvent.Status == "firing"
            ? new TriggerAlert(severity, notifyEvent.Summary)
            {
                Source = notifyEvent.Source.ToString(),
                DedupKey = dedupKey,
                CustomDetails = new { teams, service = notifyEvent.Service, environment = notifyEvent.Environment },
                Timestamp = notifyEvent.StartsAt
            }
            : new ResolveAlert(dedupKey);
    }

    private static Alert BuildGithubAlert(AlertNotification alertNotification, List<string> teams, Severity severity)
    {
        throw new NotImplementedException("Github alerts for PagerDuty are not implemented yet.");
    }

    private static string DedupKey(NotifyEvent notifyEvent)
    {
        var input = notifyEvent.DedupKey();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}