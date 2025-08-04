using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Config;
using Defra.Cdp.Notify.Backend.Api.Endpoints;
using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services;
using Defra.Cdp.Notify.Backend.Api.Services.Email;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Defra.Cdp.Notify.Backend.Api.Services.PagerDuty;
using Defra.Cdp.Notify.Backend.Api.Services.Secrets;
using Defra.Cdp.Notify.Backend.Api.Services.Slack;
using Defra.Cdp.Notify.Backend.Api.Services.Sns;
using Defra.Cdp.Notify.Backend.Api.Services.Sqs;
using Defra.Cdp.Notify.Backend.Api.Services.Teams;
using Defra.Cdp.Notify.Backend.Api.Utils;
using Defra.Cdp.Notify.Backend.Api.Utils.Http;
using Defra.Cdp.Notify.Backend.Api.Utils.Logging;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using Defra.Cdp.Notify.Backend.Api.Utils.Serialization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Quartz;
using Serilog;
using Environment = Defra.Cdp.Notify.Backend.Api.Models.Environment;

var app = CreateWebApplication(args);
await app.RunAsync();
return;

[ExcludeFromCodeCoverage]
static WebApplication CreateWebApplication(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureBuilder(builder);

    var app = builder.Build();
    return SetupApplication(app);
}

[ExcludeFromCodeCoverage]
static void ConfigureBuilder(WebApplicationBuilder builder)
{
    builder.Configuration.AddEnvironmentVariables();

    // Load certificates into Trust Store - Note must happen before Mongo and Http client connections.
    builder.Services.AddCustomTrustStore();

    // Configure logging to use the CDP Platform standards.
    builder.Services.AddHttpContextAccessor();
    builder.Host.UseSerilog(CdpLogging.Configuration);

    // Default HTTP Client
    builder.Services
        .AddHttpClient("DefaultClient")
        .AddHeaderPropagation();

    // Proxy HTTP Client
    builder.Services.AddTransient<ProxyHttpMessageHandler>();
    builder.Services
        .AddHttpClient("proxy")
        .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();

    // Propagate trace header.
    builder.Services.AddHeaderPropagation(options =>
    {
        var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });

    // Set up the MongoDB client. Config and credentials are injected automatically at runtime.
    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection(MongoConfig.ConfigKey));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();

    builder.Services.AddHealthChecks();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Set up the endpoints and their dependencies
    builder.Services.Configure<GrafanaAlertListenerConfig>(
        builder.Configuration.GetSection(GrafanaAlertListenerConfig.ConfigKey));
    builder.Services.AddSingleton<GrafanaAlertListener>();
    builder.Services.AddSingleton<IGrafanaAlertHandler, GrafanaAlertHandler>();

    builder.Services.Configure<GithubEventListenerConfig>(
        builder.Configuration.GetSection(GithubEventListenerConfig.ConfigKey));
    builder.Services.AddSingleton<GithubEventListener>();
    builder.Services.AddSingleton<IGithubEventHandler, GithubEventHandler>();
    builder.Services.AddSingleton<ISqsMessageService, SqsMessageService>();

    builder.Services.AddSingleton<INotifyEventHandler, NotifyEventHandler>();

    builder.Services.Configure<PortalBackendConfig>(builder.Configuration.GetSection(PortalBackendConfig.ConfigKey));
    builder.Services.Configure<PagerDutyConfig>(builder.Configuration.GetSection(PagerDutyConfig.ConfigKey));
    builder.Services.Configure<SlackHandlerConfig>(builder.Configuration.GetSection(SlackHandlerConfig.ConfigKey));
    builder.Services.Configure<EmailClientConfig>(builder.Configuration.GetSection(EmailClientConfig.ConfigKey));

    builder.Services.AddSingleton<IPortalBackendClient, PortalBackendClient>();
    builder.Services.AddSingleton<IPagerDutyClient, PagerDutyClient>();

    builder.Services.AddSingleton<IRulesService, RulesService>();
    builder.Services.AddSingleton<IAlertNotificationService, AlertNotificationService>();
    builder.Services.AddSingleton<IPagerDutyAlertHandler, PagerDutyAlertHandler>();
    builder.Services.AddSingleton<IPagerDutyAlertBuilder, PagerDutyAlertBuilder>();
    builder.Services.AddSingleton<ISecretsService, SecretsService>();
    builder.Services.AddSingleton<IEmailAlertHandler, EmailAlertHandler>();
    builder.Services.AddSingleton<IEmailBuilder, EmailBuilder>();
    builder.Services.AddSingleton<IEmailClient, EmailClient>();
    builder.Services.AddSingleton<ISlackAlertHandler, SlackAlertHandler>();
    builder.Services.AddSingleton<ITeamOverridesService, TeamOverridesService>();
    builder.Services.AddSingleton<IEntitiesService, EntitiesService>();
    builder.Services.AddSingleton<ITeamsService, TeamsService>();
    builder.Services.AddSingleton<ISnsPublisher, SnsPublisher>();

    builder.Services.AddAwsClients(builder.Configuration, builder.IsDevMode());

    // Quartz setup for Teams scheduler
    builder.Services.Configure<QuartzOptions>(builder.Configuration.GetSection("TeamSyncer:Scheduler"));
    builder.Services.AddQuartz(q =>
    {
        var teamSyncJobKey = new JobKey("TeamSyncer");
        q.AddJob<TeamSyncer>(opts => opts.WithIdentity(teamSyncJobKey));

        var githubInterval = builder.Configuration.GetValue<int>("TeamSyncer:PollIntervalSecs");
        q.AddTrigger(opts => opts
            .ForJob(teamSyncJobKey)
            .WithIdentity("TeamSyncer-trigger")
            .WithSimpleSchedule(d => d.WithIntervalInSeconds(githubInterval).RepeatForever().Build()));
    });
    builder.Services.AddQuartzHostedService(options =>
    {
        // when shutting down we want jobs to complete gracefully
        options.WaitForJobsToComplete = true;
    });

    builder.Services.Configure<JsonOptions>(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new EnumMemberJsonConverter<Environment>());
    });
}

[ExcludeFromCodeCoverage]
static WebApplication SetupApplication(WebApplication app)
{
    app.UseHeaderPropagation();
    app.UseRouting();
    app.MapHealthChecks("/health");

    app.MapRulesEndpoints();

    var logger = app.Services.GetService<ILogger<Program>>();

    BsonSerializer.RegisterSerializer(typeof(AlertMethod), new EnumSerializer<AlertMethod>(BsonType.String));
    BsonSerializer.RegisterSerializer(typeof(Source), new EnumSerializer<Source>(BsonType.String));
    BsonSerializer.RegisterSerializer(typeof(Environment), new EnumMemberValueEnumSerializer<Environment>());
    
    var grafanaAlertListener = app.Services.GetService<GrafanaAlertListener>();
    logger?.LogInformation("Starting GrafanaAlertListener - reading alerts from SQS");
    Task.Run(() => grafanaAlertListener?.ReadAsync(app.Lifetime.ApplicationStopping));

    var githubEventListener = app.Services.GetService<GithubEventListener>();
    logger?.LogInformation("Starting GithubEventListener - reading events from SQS");
    Task.Run(() => githubEventListener?.ReadAsync(app.Lifetime.ApplicationStopping));

    return app;
}