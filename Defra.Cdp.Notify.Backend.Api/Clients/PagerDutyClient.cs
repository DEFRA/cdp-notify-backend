using System.Security.Cryptography;
using System.Text;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Models;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Requests;

namespace Defra.Cdp.Notify.Backend.Api.Clients;

public interface IPagerDutyClient
{
    public Task SendAlert(string integrationKey, Alert alert);
}

public class PagerDutyClient(HttpClient httpClient, IOptions<PagerDutyConfig> config, ILogger<PagerDutyClient> logger)
    : IPagerDutyClient
{
    public async Task SendAlert(string integrationKey, Alert alert)
    {
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