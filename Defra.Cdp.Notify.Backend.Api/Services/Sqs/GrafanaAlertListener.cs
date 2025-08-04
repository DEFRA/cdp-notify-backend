using Amazon.SQS;
using Defra.Cdp.Notify.Backend.Api.Config;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Notify.Backend.Api.Services.Sqs;

public class GrafanaAlertListener(
    IAmazonSQS sqs,
    IOptions<GrafanaAlertListenerConfig> config,
    IGrafanaAlertHandler grafanaAlertHandler,
    ILogger<GrafanaAlertListener> logger
) : SqsListener(sqs, grafanaAlertHandler, config.Value.QueueUrl, logger, config.Value.Enabled);
