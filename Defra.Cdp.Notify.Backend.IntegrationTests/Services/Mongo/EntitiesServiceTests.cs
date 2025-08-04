using Defra.Cdp.Notify.Backend.Api.Clients;
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

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services.Mongo;

public class EntitiesServiceTests(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task ShouldInsertUpdateAndRemoveEntriesAppropriately()
    {
        var mongoConfig = Substitute.For<IOptions<MongoConfig>>();
        mongoConfig.Value.Returns(new MongoConfig
        {
            DatabaseName = "EntitiesServiceTests", DatabaseUri = Fixture.ConnectionString
        });
        var mongoFactory = new MongoDbClientFactory(mongoConfig);
        var loggerFactory = new LoggerFactory();
        var entitiesService = new EntitiesService(mongoFactory, loggerFactory);
        
        var entity1 = new Entity("TestEntity1", [new PortalBackendTeam("team1", "Team-1")]);
        await entitiesService.PersistAll([entity1], CancellationToken.None);
        var entities1 = await entitiesService.GetEntities();
        
        Assert.Single(entities1);
        Assert.Equal("TestEntity1", entities1[0].Name);
        Assert.Single(entities1[0].Teams);
        
        var entity1WithExtraTeam = new Entity("TestEntity1", [new PortalBackendTeam("team1", "Team-1"), new PortalBackendTeam("team2", "Team-2")]);
        await entitiesService.PersistAll([entity1WithExtraTeam], CancellationToken.None);
        var entities2 = await entitiesService.GetEntities();
        
        Assert.Single(entities2);
        Assert.Equal("TestEntity1", entities2[0].Name);
        Assert.Equal(2, entities2[0].Teams.Length);
        
        var entity2 = new Entity("TestEntity2", [new PortalBackendTeam("team2", "Team-3")]);
        await entitiesService.PersistAll([entity1WithExtraTeam, entity2], CancellationToken.None);
        var entities3 = await entitiesService.GetEntities();
        
        Assert.Equal(2, entities3.Count);
        Assert.Equal("TestEntity1", entities3[0].Name);
        Assert.Equal("TestEntity2", entities3[1].Name);
        Assert.Equal(2, entities3[0].Teams.Length);
        
        await entitiesService.PersistAll([entity2], CancellationToken.None);
        var entities4 = await entitiesService.GetEntities();
        
        Assert.Single(entities4);
        Assert.Equal("TestEntity2", entities4[0].Name);
        Assert.Single(entities4[0].Teams);
    }
}