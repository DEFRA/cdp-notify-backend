using Amazon.SQS;
using Defra.Cdp.Notify.Backend.Api.Config;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sqs;

public class GithubEventListener(
    IAmazonSQS sqs,
    IOptions<GithubEventListenerConfig> config,
    IGithubEventHandler githubEventHandler,
    ILogger<GithubEventListener> logger
) : SqsListener(sqs, githubEventHandler, config.Value.QueueUrl, logger, config.Value.Enabled);
