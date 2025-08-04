namespace Defra.Cdp.Notify.Backend.Api.Services.Slack;

public static class SlackMessageBuilder
{
    public static object BuildSlackMessage(
        string slackChannel,
        string workflowName,
        string repo,
        string workflowUrl,
        long runNumber,
        string commitMessage,
        string author)
    {
        return new
        {
            team = "platform",
            slack_channel = slackChannel,
            message  = new
            {
                channel = slackChannel,
                attachments = new[]
                {
                    new
                    {
                        color = "#f03f36",
                        blocks = new object[]
                        {
                            new
                            {
                                type = "context",
                                elements =
                                    new object[]
                                    {
                                        new
                                        {
                                            type = "image",
                                            image_url =
                                                "https://www.iconfinder.com/icons/298822/download/png/512",
                                            alt_text = "GitHub"
                                        },
                                        new
                                        {
                                            type = "mrkdwn",
                                            text = "*Failed GitHub Action*"
                                        }
                                    }
                            },
                            new
                            {
                                type = "rich_text",
                                elements = new object[]
                                {
                                    new
                                    {
                                        type = "rich_text_section",
                                        elements = new object[]
                                        {
                                            new
                                            {
                                                type = "text",
                                                text =
                                                    $"{repo} - '{workflowName}' failed",
                                                style = new { bold = true }
                                            }
                                        }
                                    }
                                }
                            },
                            new
                            {
                                type = "context",
                                elements = new object[]
                                {
                                    new
                                    {
                                        type = "mrkdwn",
                                        text =
                                            $"*Failed Workflow:* <{workflowUrl}|{runNumber}>"
                                    }
                                }
                            },
                            new
                            {
                                type = "context",
                                elements = new object[]
                                {
                                    new
                                    {
                                        type = "mrkdwn",
                                        text =
                                            $"*Commit Message:* '{commitMessage}'\n*Author:* {author}"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}