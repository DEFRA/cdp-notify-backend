using Defra.Cdp.Notify.Backend.IntegrationTests.Services;
using Xunit;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Utils;

public abstract class ServiceTest : IClassFixture<MongoIntegrationTest>
{
    protected readonly MongoIntegrationTest Fixture;

    protected ServiceTest(MongoIntegrationTest fixture)
    {
        Fixture = fixture;

        Task.Run(() => Fixture.InitializeAsync()).Wait();
    }
}