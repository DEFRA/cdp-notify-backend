using Defra.Cdp.Notify.Backend.Api.Clients;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using Quartz;

namespace Defra.Cdp.Notify.Backend.Api.Services.Teams;

public sealed class TeamSyncer(
    IPortalBackendClient portalBackendClient,
    IEntitiesService entitiesService,
    ITeamsService teamsService,
    ILogger<TeamSyncer> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Syncing entities & teams from portal backend...");
        var entities = await portalBackendClient.GetEntities(context.CancellationToken);
        await entitiesService.PersistAll(entities, context.CancellationToken);
        
        var allTeamNames = entities
            .SelectMany(e => e.Teams)
            .Select(t => t.Name)
            .Distinct()
            .ToHashSet();
        
        await teamsService.PersistAll(allTeamNames, context.CancellationToken);
    }
}