using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sqs;


public interface IGrafanaAlertHandler : ISqsMessageHandler;

public class GrafanaAlertHandler(
    INotifyEventHandler notifyEventHandler,
    ISqsMessageService sqsMessageService,
    ILogger<GrafanaAlertListener> logger
) : SqsMessageHandler<GrafanaEventAlert>(Source.Grafana, sqsMessageService, notifyEventHandler, logger), IGrafanaAlertHandler
{
    protected override NotifyEvent? AlertToNotifyEvent(GrafanaEventAlert alert, Message message)
    {
        if (alert.Service == null)
        {
            logger.LogWarning("Grafana alert does not have a service defined. Message({MessageId}): {MessageBody}",
                message.MessageId, message.Body);
            return null;
        }

        GrafanaAlertStatus[] validStatuses = [GrafanaAlertStatus.firing, GrafanaAlertStatus.resolved];

        if (!validStatuses.Contains(alert.Status!))
        {
            logger.LogWarning("Grafana alert status is not firing or resolved. Message({MessageId}): {MessageBody}",
                message.MessageId, message.Body);
            return null;
        }

        return alert.ToNotifyEvent(message.MessageId);
    }
}
