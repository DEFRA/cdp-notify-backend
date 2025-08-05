using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Utils.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Notify.Backend.Api.Services.Mongo;

public interface IRulesService
{
    Task<AlertRule?> GetAlertRuleForGrafanaEvent(GrafanaNotifyEvent notifyEvent, CancellationToken cancellationToken);
    Task<AlertRule?> GetAlertRuleForGithubEvent(GithubNotifyEvent notifyEvent, CancellationToken cancellationToken);

    Task<List<AlertRule>> GetAlertRules(CancellationToken cancellationToken);

    Task<bool> PersistRule(AlertRule alertRule, CancellationToken cancellationToken);
}

public class RulesService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<AlertRule>(connectionFactory, CollectionName, loggerFactory), IRulesService
{
    private const string CollectionName = "alertrules";

    protected override List<CreateIndexModel<AlertRule>> DefineIndexes(IndexKeysDefinitionBuilder<AlertRule> builder)
    {
        var keys = builder.Ascending(r => r.Source)
            .Ascending(r => r.Environment)
            .Ascending(r => r.Service)
            .Ascending(r => r.PagerDuty);

        var index = new CreateIndexModel<AlertRule>(keys, new CreateIndexOptions { Unique = true });

        return [index];
    }

    public async Task<List<AlertRule>> GetAlertRules(CancellationToken cancellationToken)
    {
        return await Collection.Find(Builders<AlertRule>.Filter.Empty).ToListAsync(cancellationToken);
    }

    public async Task<AlertRule?> GetAlertRuleForGithubEvent(GithubNotifyEvent notifyEvent, CancellationToken cancellationToken)
    {
        var builder = Builders<AlertRule>.Filter;

        var baseFilter = builder.And(
            builder.Eq(r => r.Source, notifyEvent.Source)
        );
        var filters = new List<FilterDefinition<AlertRule>>
        {
            builder.And(
                baseFilter,
                builder.Eq(r => r.Service, notifyEvent.Service)
            ),

            builder.And(
                baseFilter,
                builder.Eq(r => r.Service, null)
            )
        };


        foreach (var filter in filters)
        {
            var result = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (result is not null)
                return result;
        }

        return null;
    }

    public async Task<AlertRule?> GetAlertRuleForGrafanaEvent(GrafanaNotifyEvent notifyEvent, CancellationToken cancellationToken)
    {
        var builder = Builders<AlertRule>.Filter;

        var baseFilter = builder.And(
            builder.Eq(r => r.Source, notifyEvent.Source),
            builder.Eq(r => r.PagerDuty, notifyEvent.PagerDuty)
        );
        var filters = new List<FilterDefinition<AlertRule>>
        {
            builder.And(
                baseFilter,
                builder.Eq(r => r.Environment, notifyEvent.Environment),
                builder.Eq(r => r.Service, notifyEvent.Service)
            ),

            builder.And(
                baseFilter,
                builder.Eq(r => r.Environment, notifyEvent.Environment),
                builder.Eq(r => r.Service, null)
            ),

            builder.And(
                baseFilter,
                builder.Eq(r => r.Environment, null),
                builder.Eq(r => r.Service, null)
            )
        };


        foreach (var filter in filters)
        {
            var result = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (result is not null)
                return result;
        }

        return null;
    }

    public async Task<bool> PersistRule(AlertRule alertRule, CancellationToken cancellationToken)
    {
        try
        {
            await Collection.InsertOneAsync(alertRule, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to insert {AlertRule}", alertRule);
            return false;
        }
    }
}