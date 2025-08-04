using Testcontainers.MongoDb;

namespace Defra.Cdp.Notify.Backend.IntegrationTests.Services;

public class MongoIntegrationTest : IAsyncDisposable
{
    public required string ConnectionString { get; set; }
    private MongoDbContainer? _mongoDbContainer;

    public async Task InitializeAsync()
    {
        if (_mongoDbContainer != null)
        {
            await _mongoDbContainer.DisposeAsync();
        }

        // Initialize MongoDB container with a specific version
        _mongoDbContainer = new MongoDbBuilder()
            .WithImage("mongo:6.0")
            .Build();

        // Start the container
        await _mongoDbContainer.StartAsync();

        ConnectionString = _mongoDbContainer.GetConnectionString();
    }


    public async ValueTask DisposeAsync()
    {
        if (_mongoDbContainer != null)
        {
            await _mongoDbContainer.StopAsync();
        }
        GC.SuppressFinalize(this);
    }
}