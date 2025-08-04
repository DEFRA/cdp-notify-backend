using Defra.Cdp.Notify.Backend.Api.Models;
using Defra.Cdp.Notify.Backend.Api.Services.Mongo;
using FluentValidation.Results;

namespace Defra.Cdp.Notify.Backend.Api.Endpoints;

public static class RulesEndpoints
{
    private const string EndpointName = "rules";
    public static void MapRulesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(EndpointName, Create);
        app.MapGet(EndpointName, GetAll);
    }

    private static async Task<IResult> Create(
        AlertRule rule, IRulesService rulesService, CancellationToken cancellationToken)
    {
        var created = await rulesService.PersistRule(rule, cancellationToken);
        if (!created)
            return Results.BadRequest(new List<ValidationFailure>
            {
                new("Rule", "Rule could not be created, it may already exist or there was a database error.")
            });

        return Results.Created($"/${EndpointName}/{rule.Id}", rule);
    }

    private static async Task<IResult> GetAll(IRulesService rulesService, CancellationToken cancellationToken)
    {
        var matches = await rulesService.GetAlertRules(cancellationToken);
        return Results.Ok(matches);
    }
}