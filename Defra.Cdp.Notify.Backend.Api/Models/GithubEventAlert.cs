using System.Text.Json.Serialization;

namespace Defra.Cdp.Notify.Backend.Api.Models;

public class GithubEventAlert : IAlert
{
    [property: JsonPropertyName("github_event")] public required string GithubEvent { get; set; }
    [property: JsonPropertyName("action")] public required string Action { get; set; }
    [property: JsonPropertyName("repository")] public required Repository Repository { get; set; }
    [property: JsonPropertyName("workflow_run")] public required WorkflowRun WorkflowRun { get; set; }

    public GithubNotifyEvent ToNotifyEvent()
    {
        return new GithubNotifyEvent(
            Repository.Name,
            WorkflowRun.Name,
            WorkflowRun.Conclusion, 
            WorkflowRun.HtmlUrl, 
            WorkflowRun.RunNumber,
            WorkflowRun.HeadCommit.Message,
            WorkflowRun.HeadCommit.Author.Name);
    }
}

public class WorkflowRun
{
    [property: JsonPropertyName("name")] public required string Name { get; set; }
    [property: JsonPropertyName("html_url")] public required string HtmlUrl { get; set; }
    [property: JsonPropertyName("run_number")] public required long RunNumber { get; set; }
    [property: JsonPropertyName("head_branch")] public required string HeadBranch { get; set; }
    [property: JsonPropertyName("head_commit")] public required Commit HeadCommit { get; set; }
    [property: JsonPropertyName("conclusion")] public required string Conclusion { get; set; }
}

public class Commit
{
    [property: JsonPropertyName("message")] public required string Message { get; set; }
    [property: JsonPropertyName("author")] public required Author Author { get; set; }

}

public class Author
{
    [property: JsonPropertyName("name")] public required string Name { get; set; }

}

public class Repository
{
    [property: JsonPropertyName("name")] public required string Name { get; set; }
    [property: JsonPropertyName("html_url")] public required string HtmlUrl { get; set; }
}