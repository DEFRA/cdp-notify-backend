using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Defra.Cdp.Notify.Backend.Api.Services.Secrets;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Defra.Cdp.Notify.Backend.Tests.Services.Secrets;

public class SecretsServiceTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();
    private readonly ILogger<SecretsService> _logger = Substitute.For<ILogger<SecretsService>>();
    private readonly SecretsService _service;

    public SecretsServiceTests()
    {
        _service = new SecretsService(_secretsManager, _logger);
    }

    [Fact]
    public async Task PagerDutyIntegrationKey_ReturnsSecret_WhenFound()
    {
        const string team = "team-a";
        var secretValue = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { team, "key-123" }
        });

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = secretValue });

        var result = await _service.PagerDutyIntegrationKey(team, CancellationToken.None);

        Assert.Equal("key-123", result);
    }

    [Fact]
    public async Task PagerDutyIntegrationKey_ReturnsNull_WhenTeamNotFound()
    {
        var secretValue = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "other-team", "value" }
        });

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = secretValue });

        var result = await _service.PagerDutyIntegrationKey("team-x", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PagerDutyIntegrationKey_ReturnsNull_WhenSecretsManagerThrows()
    {
        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Simulated AWS error"));

        var result = await _service.PagerDutyIntegrationKey("team-a", CancellationToken.None);

        Assert.Null(result);
    }
}