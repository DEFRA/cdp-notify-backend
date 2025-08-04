using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Notify.Backend.Api.Models;

[BsonDiscriminator]
[BsonKnownTypes(typeof(GithubNotifyEvent), typeof(GrafanaNotifyEvent))]
public abstract record NotifyEvent(
    Source Source,
    string Service,
    string Status)
{
    public abstract string DedupKey();
}

public record GithubNotifyEvent(
    string Service,
    string WorkflowName,
    string Conclusion,
    string WorkflowUrl,
    long RunNumber,
    string CommitMessage,
    string Author) :
    NotifyEvent(Source.Github, Service, Conclusion)
{
    public override string DedupKey()
    {
        return $"{Service}_{WorkflowUrl}_{RunNumber}";
    }
}

public record GrafanaNotifyEvent(
    Environment Environment,
    string Service,
    GrafanaAlertStatus GrafanaStatus,
    string? AlertName,
    string Summary,
    string AlertUrl,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool PagerDuty) :
    NotifyEvent(Source.Grafana, Service, GrafanaStatus.ToString())
{
    public override string DedupKey()
    {
        return $"{Service}_{Environment}_{Source}_{AlertUrl}";
    }
}