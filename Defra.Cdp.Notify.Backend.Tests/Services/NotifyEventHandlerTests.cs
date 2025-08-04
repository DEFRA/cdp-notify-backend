using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Email;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;
using Defra.Cdp.Notify.Backend.Api.Services.Slack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Environment = Defra.Cdp.Notify.Backend.Api.Models.Environment;

namespace Defra.Cdp.Notify.Backend.Tests.Services;

public class NotifyEventHandlerTests
{
    [Fact]
    public async Task Handle_CallsPagerDutyHandler_WhenRuleIsPagerDutyAndEventPagerDutyTrue()
    {
        var rulesService = Substitute.For<IRulesService>();
        var pagerDutyHandler = Substitute.For<IPagerDutyAlertHandler>();
        var emailHandler = Substitute.For<IEmailAlertHandler>();
        var slackHandler = Substitute.For<ISlackAlertHandler>();
        var logger = Substitute.For<ILogger<NotifyEventHandler>>();

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Test,
            "TestService",
            GrafanaAlertStatus.firing,
            "Test event",
            "Some summary",
            "http://example.com/alert/12345",
            DateTime.Now,
            DateTime.Now,
            true
        );
        var alertRule = new AlertRule (Source.Grafana, null, null, true, [AlertMethod.PagerDuty]);
        rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None).Returns(alertRule);

        var handler = new NotifyEventHandler(
            rulesService,
            pagerDutyHandler,
            emailHandler,
            slackHandler,
            logger
        );

        await handler.Handle(notifyEvent, CancellationToken.None);

        await pagerDutyHandler.Received(1).Handle(notifyEvent, alertRule, Arg.Any<CancellationToken>());
        await emailHandler.DidNotReceive().Handle(Arg.Any<NotifyEvent>(), Arg.Any<AlertRule>(), Arg.Any<CancellationToken>());
        await slackHandler.DidNotReceive().Handle(Arg.Any<NotifyEvent>(), Arg.Any<AlertRule>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CallsEmailHandler_WhenRuleIsEmail()
    {
        var rulesService = Substitute.For<IRulesService>();
        var pagerDutyHandler = Substitute.For<IPagerDutyAlertHandler>();
        var emailHandler = Substitute.For<IEmailAlertHandler>();
        var slackHandler = Substitute.For<ISlackAlertHandler>();
        var logger = Substitute.For<ILogger<NotifyEventHandler>>();

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Test,
            "Test event",
            GrafanaAlertStatus.resolved,
            "TestService",
            "Something or other",
            "http://example.com/alert/12345",
            DateTime.Now,
            DateTime.Now,
            true
        );
        var alertRule = new AlertRule (Source.Grafana, null, null, null,[AlertMethod.Email]);
        rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None).Returns(alertRule);

        var handler = new NotifyEventHandler(
            rulesService,
            pagerDutyHandler,
            emailHandler,
            slackHandler,
            logger
        );

        await handler.Handle(notifyEvent, CancellationToken.None);

        await emailHandler.Received(1).Handle(notifyEvent, alertRule, Arg.Any<CancellationToken>());
        await pagerDutyHandler.DidNotReceiveWithAnyArgs().Handle(null!, null!, CancellationToken.None);
        await slackHandler.DidNotReceiveWithAnyArgs().Handle(null!, null!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_CallsSlackHandler_WhenRuleIsSlack()
    {
        var rulesService = Substitute.For<IRulesService>();
        var pagerDutyHandler = Substitute.For<IPagerDutyAlertHandler>();
        var emailHandler = Substitute.For<IEmailAlertHandler>();
        var slackHandler = Substitute.For<ISlackAlertHandler>();
        var logger = Substitute.For<ILogger<NotifyEventHandler>>();

        var notifyEvent = new GrafanaNotifyEvent
        (
            Environment.Test,
            "TestService",
            GrafanaAlertStatus.firing,
            "Test event",
            "Some summary",
            "Critical",
            DateTime.Now,
            DateTime.Now,
            true
        );
        var alertRule = new AlertRule (Source.Grafana, null, null, null, [AlertMethod.Slack]);
        rulesService.GetAlertRuleForGrafanaEvent(notifyEvent, CancellationToken.None).Returns(alertRule);

        var handler = new NotifyEventHandler(
            rulesService,
            pagerDutyHandler,
            emailHandler,
            slackHandler,
            logger
        );

        await handler.Handle(notifyEvent, CancellationToken.None);

        await slackHandler.Received(1).Handle(notifyEvent, alertRule, Arg.Any<CancellationToken>());
        await pagerDutyHandler.DidNotReceiveWithAnyArgs().Handle(null!, null!, CancellationToken.None);
        await emailHandler.DidNotReceiveWithAnyArgs().Handle(null!, null!, CancellationToken.None);
    }
}