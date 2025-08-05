using Amazon.SQS;
using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Email;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;
using Defra.Cdp.Notify.Backend.Api.Services.Secrets;
using Defra.Cdp.Notify.Backend.Api.Services.Slack;
using Defra.Cdp.Notify.Backend.Api.Services.Sns;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using Defra.Cdp.Notify.Backend.IntegrationTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services;

public abstract class EndToEndTestBase : ServiceTest
{
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly AlertNotificationService AlertNotificationService;
    protected readonly EntitiesService EntitiesService;
    protected readonly TeamsService TeamsService;
    protected readonly SqsMessageService SqsMessageService;
    protected readonly RulesService RulesService;
    protected readonly IAmazonSQS Sqs;
    protected readonly ISnsPublisher SnsPublisher;
    protected readonly INotifyEventHandler NotifyHandler;
    protected readonly ISecretsService SecretsService;
    protected readonly IPagerDutyClient PagerDutyClient;
    protected const string TestSlackArn = "test-slack-arn";

    protected EndToEndTestBase(MongoIntegrationTest fixture) : base(fixture)
    {
        var mongoConfig = Substitute.For<IOptions<MongoConfig>>();
        mongoConfig.Value.Returns(new MongoConfig
        {
            DatabaseName = GetType().Name, DatabaseUri = Fixture.ConnectionString
        });
        var mongoFactory = new MongoDbClientFactory(mongoConfig);
        
        Sqs = Substitute.For<IAmazonSQS>();
        LoggerFactory = Substitute.For<ILoggerFactory>();
        

        AlertNotificationService = new AlertNotificationService(mongoFactory, LoggerFactory);
        var teamOverridesService = new TeamOverridesService(mongoFactory, LoggerFactory);
        EntitiesService = new EntitiesService(mongoFactory, LoggerFactory);

        SecretsService = Substitute.For<ISecretsService>();
        PagerDutyClient = Substitute.For<IPagerDutyClient>();
        var pagerDutyAlertHandler = new PagerDutyAlertHandler(
            PagerDutyClient,
            AlertNotificationService,
            EntitiesService,
            SecretsService,
            teamOverridesService,
            new PagerDutyAlertBuilder(),
            new Logger<PagerDutyAlertHandler>(LoggerFactory));

        TeamsService = new TeamsService(mongoFactory, LoggerFactory);

        var emailAlertHandler = new EmailAlertHandler(AlertNotificationService, teamOverridesService, EntitiesService,
            TeamsService,
            new EmailBuilder(),
            Substitute.For<IEmailClient>(),
            new Logger<EmailAlertHandler>(LoggerFactory)
        );

        SnsPublisher = Substitute.For<ISnsPublisher>();
        var slackHandlerConfig = Substitute.For<IOptions<SlackHandlerConfig>>();
        
        slackHandlerConfig.Value.Returns(new SlackHandlerConfig
        {
            TopicArn = TestSlackArn
        });
        var slackAlertHandler = new SlackAlertHandler(AlertNotificationService, teamOverridesService, EntitiesService,
            TeamsService,
            SnsPublisher,
            slackHandlerConfig,
            new Logger<SlackAlertHandler>(LoggerFactory));

        RulesService = new RulesService(mongoFactory, LoggerFactory);
        NotifyHandler = new NotifyEventHandler(
            RulesService,
            pagerDutyAlertHandler,
            emailAlertHandler,
            slackAlertHandler,
            new Logger<NotifyEventHandler>(LoggerFactory)
        );

        SqsMessageService = new SqsMessageService(mongoFactory, LoggerFactory);

    }
}