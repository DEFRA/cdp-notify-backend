using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using Defra.Cdp.Notify.Backend.IntegrationTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Environment = Defra.Cdp.Notify.Backend.Api.Models.Environment;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services.Mongo;

public class RulesServiceTests(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task ShouldReturnPagerDutyRule_WhenGrafanaEventIsPagerDuty()
    {
        var rulesService = await InitialiseRulesServiceForTest([
            new AlertRule(Source.Grafana, null, null, true, [AlertMethod.PagerDuty]),
            new AlertRule(Source.Grafana, null, null, false, [AlertMethod.Email])
        ]);

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Test,
            "TestService",
            GrafanaAlertStatus.firing,
            "Alert name",
            "Critical",
            "Test event",
            DateTime.Now,
            DateTime.Now,
            true
        );

        var result = await rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Methods);
        Assert.Contains(AlertMethod.PagerDuty, result.Methods);
    }

    [Fact]
    public async Task ShouldReturnRuleMatchingSourcePagerDutyAndEnvironment_WhenNoMoreSpecificGrafanaRuleExists()
    {
        var rulesService = await InitialiseRulesServiceForTest([
            new AlertRule(Source.Grafana, Environment.Prod, null, true, [AlertMethod.PagerDuty]),
            new AlertRule(Source.Grafana, Environment.Prod, "another-service", true, [AlertMethod.Slack]),
            new AlertRule(Source.Grafana, null, null, false, [AlertMethod.Email])
        ]);

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Prod,
            "TestService",
            GrafanaAlertStatus.firing,
            "Alert name",
            "Critical",
            "Test event",
            DateTime.Now,
            DateTime.Now,
            true
        );

        var result = await rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Methods);
        Assert.Contains(AlertMethod.PagerDuty, result.Methods);
    }

    [Fact]
    public async Task ShouldReturnRuleThatMatchesAllFieldsIfExists()
    {
        var rulesService = await InitialiseRulesServiceForTest([
            new AlertRule(Source.Grafana, null, null, true, [AlertMethod.PagerDuty]),
            new AlertRule(Source.Grafana, Environment.Test, "TestService", true, [AlertMethod.Slack])
            ]
        );

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Test,
            "TestService",
            GrafanaAlertStatus.firing,
            "Alert name",
            "Critical",
            "Test event",
            DateTime.Now,
            DateTime.Now,
            true
        );

        var result = await rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Methods);
        Assert.Contains(AlertMethod.Slack, result.Methods);
    }

    [Fact]
    public async Task ShouldReturnGrafanaRuleThatMatchesJustSourceAndPagerDutyIfNoMoreSpecificMatch()
    {
        var rulesService = await InitialiseRulesServiceForTest([
            new AlertRule(Source.Github, null, null, null, [AlertMethod.Email, AlertMethod.Slack]),
            new AlertRule(Source.Grafana, null, null, false, [AlertMethod.Email]),
            new AlertRule(Source.Github, Environment.Test, "NotMyTestService", null, [AlertMethod.Slack]),
            new AlertRule(Source.Github, Environment.Exttest, "MyTestService", null, [AlertMethod.Slack])
        ]);

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Dev,
            "TestService",
            GrafanaAlertStatus.firing,
            "Alert name",
            "Alert Firing",
            "https://example.com/alert",
            DateTime.Now,
            DateTime.Now,
            false
        );

        var result = await rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Methods);
        Assert.Equal([AlertMethod.Email], result.Methods);
    }

    [Fact]
    public async Task ShouldReturnGithubRuleThatMatchesSourceAndService()
    {
        var rulesService = await InitialiseRulesServiceForTest([
            new AlertRule(Source.Github, null, null, null, [AlertMethod.Email, AlertMethod.Slack]),
            new AlertRule(Source.Grafana, null, null, false, [AlertMethod.Email]),
            new AlertRule(Source.Github, null, "NotMyTestService", null, [AlertMethod.Slack]),
            new AlertRule(Source.Github, null, "TestService", null, [AlertMethod.PagerDuty])
        ]);

        var notifyEvent = new GithubNotifyEvent
        (
            "TestService",
            "Create Workflow",
            "success",
            "https://example.com/workflow",
            123,
            "I made a change",
            "Mr Coder"
        );

        var result = await rulesService.GetAlertRuleForGithubEvent(notifyEvent, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Methods);
        Assert.Equal([AlertMethod.PagerDuty], result.Methods);
    }

    [Fact]
    public async Task ShouldReturnGithubRuleThatMatchesSource()
    {
        var rulesService = await InitialiseRulesServiceForTest([
            new AlertRule(Source.Github, null, null, null, [AlertMethod.Email, AlertMethod.Slack]),
            new AlertRule(Source.Grafana, null, null, false, [AlertMethod.Email]),
            new AlertRule(Source.Github, null, "NotMyTestService", null, [AlertMethod.Slack]),
            new AlertRule(Source.Github, null, "TestService", null, [AlertMethod.PagerDuty])
        ]);

        var notifyEvent = new GithubNotifyEvent
        (
            "AnotherService",
            "Create Workflow",
            "success",
            "https://example.com/workflow",
            123,
            "I made a change",
            "Mr Coder"
        );

        var result = await rulesService.GetAlertRuleForGithubEvent(notifyEvent, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Methods.Count);
        Assert.Equal([AlertMethod.Email, AlertMethod.Slack], result.Methods);
    }

    private async Task<RulesService> InitialiseRulesServiceForTest(List<AlertRule> alertRules)
    {
        var mongoConfig = Substitute.For<IOptions<MongoConfig>>();
        mongoConfig.Value.Returns(new MongoConfig
        {
            DatabaseName = "RulesServiceTests", DatabaseUri = Fixture.ConnectionString
        });
        var mongoFactory = new MongoDbClientFactory(mongoConfig);
        var loggerFactory = new LoggerFactory();
        var rulesService = new RulesService(mongoFactory, loggerFactory);

        foreach (var rule in alertRules)
        {
            await rulesService.PersistRule(rule, CancellationToken.None);
        }

        var persistedRules = await rulesService.GetAlertRules(CancellationToken.None);

        Assert.Equal(alertRules.Count, persistedRules.Count);
        return rulesService;
    }
}