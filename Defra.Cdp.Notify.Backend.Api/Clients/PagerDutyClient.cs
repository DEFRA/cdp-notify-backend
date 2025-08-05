using Defra.Cdp.Notify.Backend.Api.Config;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Requests;

namespace Defra.Cdp.Notify.Backend.Api.Clients;

public interface IPagerDutyClient
{
    public Task SendAlert(string integrationKey, Alert alert, CancellationToken cancellationToken);
}

public class PagerDutyClient(
    HttpClient httpClient,
    IOptions<PagerDutyConfig> config,
    IPortalBackendClient portalBackendClient,
    ILogger<PagerDutyClient> logger)
    : IPagerDutyClient
{
    public async Task SendAlert(string integrationKey, Alert alert, CancellationToken cancellationToken)
    {
        if (await portalBackendClient.IsFeatureToggleActive("disable-notify-publish", cancellationToken))
        {
            logger.LogInformation("Feature toggle 'disable-notify-publish' is active, skipping sending PagerDuty alert.");
            return;
        }

        var pagerDuty = new PagerDuty(integrationKey);
        pagerDuty.HttpClient = httpClient;
        pagerDuty.BaseUrl = new Uri(config.Value.Url + "/v2/");
        await SendAlert(pagerDuty, alert);
    }

    private async Task SendAlert(PagerDuty pagerDuty, Alert alert)
    {
        var resp = await pagerDuty.Send(alert);
        if (resp.Status == "success")
        {
            logger.LogInformation("Alert sent. Dedup key: {RespDedupKey}", resp.DedupKey);
        }
        else
        {
            logger.LogError("Failed to send alert: {RespMessage}", resp.Message);
        }
    }
}