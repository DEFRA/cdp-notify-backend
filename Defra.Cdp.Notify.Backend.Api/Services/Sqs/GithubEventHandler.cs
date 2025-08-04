using Amazon.SQS.Model;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sqs;


public interface IGithubEventHandler : ISqsMessageHandler;

public class GithubEventHandler(
    INotifyEventHandler notifyEventHandler,
    ISqsMessageService sqsMessageService,
    ILogger<GithubEventHandler> logger
) : SqsMessageHandler<GithubEventAlert>(Source.Github, sqsMessageService, notifyEventHandler, logger), IGithubEventHandler
{
    private readonly HashSet<string> _failedConclusions =
    [
        "action_required",
        "failure",
        "stale",
        "timed_out",
        "startup_failure"
    ];
    
    protected override GithubNotifyEvent? AlertToNotifyEvent(GithubEventAlert alert, Message message)
    {
        if (alert.GithubEvent != "workflow_run")
        {
            logger.LogWarning("Github event is not workflow_run, ignoring. Message({MessageId}): {GithubEvent}",
                message.MessageId, alert.GithubEvent);
            return null;
        }
        
        var shouldSendAlert = alert.WorkflowRun.HeadBranch == "main" 
                              && alert.Action == "completed"
                              && _failedConclusions.Contains(alert.WorkflowRun.Conclusion);

        if (!shouldSendAlert)
        {
            logger.LogWarning("Not sending alert for Github event. Message({MessageId}): {GithubEvent}, Branch: {Branch}, Action: {Action}, Conclusion: {Conclusion}",
                message.MessageId, alert.GithubEvent, alert.WorkflowRun.HeadBranch, alert.Action, alert.WorkflowRun.Conclusion);
            return null;
        }

        return alert.ToNotifyEvent();
    }
}
