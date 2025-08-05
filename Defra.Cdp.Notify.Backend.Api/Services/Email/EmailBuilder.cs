using Defra.Cdp.Notify.Backend.Api.Models;
using Scriban;

namespace Defra.Cdp.Notify.Backend.Api.Services.Email;

public interface IEmailBuilder
{
    Email BuildEmail(AlertNotification alertNotification, HashSet<string> emailAddresses);
}

public class EmailBuilder : IEmailBuilder
{
    public Email BuildEmail(AlertNotification alertNotification, HashSet<string> emailAddresses)
    {
        return alertNotification.NotifyEvent switch
        {
            GrafanaNotifyEvent notifyEvent => BuildGrafanaEmail(notifyEvent, emailAddresses),
            GithubNotifyEvent notifyEvent => BuildGithubEmail(notifyEvent, emailAddresses),
            _ => throw new ArgumentException("Unsupported NotifyEvent type {}", alertNotification.NotifyEvent.GetType().Name)
        };
        
    }


    private static Email BuildGrafanaEmail(GrafanaNotifyEvent notifyEvent, HashSet<string> emailAddresses)
    {
        var layout = Template.Parse(File.ReadAllText("Services/Email/Templates/Layouts/email.scriban"));
        var content = Template.Parse(File.ReadAllText("Services/Email/Templates/grafana-alert.scriban"));

        var statusColour = notifyEvent.GrafanaStatus switch
        {
            GrafanaAlertStatus.firing => "#d4351C",
            GrafanaAlertStatus.resolved => "#00703c",
            _ => throw new ArgumentOutOfRangeException(nameof(notifyEvent))
        };
        
        var context = new
        {
            statusColour,
            status = notifyEvent.Status,
            service = notifyEvent.Service,
            environment = notifyEvent.Environment,
            alertName = notifyEvent.AlertName,
            startsAt = notifyEvent.StartsAt,
            endsAt = notifyEvent.EndsAt,
            summary = notifyEvent.Summary,
            alertURL = notifyEvent.AlertUrl,
        };

        var inner = content.Render(context);
        var full = layout.Render(new { content = inner });

        var subject = "Alert Triggered" + (notifyEvent.AlertName != null ? " " + notifyEvent.AlertName : "");
        
        return new Email(subject, full, emailAddresses);
    }
    
    private static Email BuildGithubEmail(GithubNotifyEvent notifyEvent, HashSet<string> emailAddresses)
    {
        throw new NotImplementedException();
    }
}

public record Email(
    string Subject,
    string Body,
    HashSet<string> ToAddresses
);


