using System.Text;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace Defra.Cdp.Notify.Backend.Api.Services.Secrets;

public interface ISecretsService
{
    Task<string?> PagerDutyIntegrationKey(string team, CancellationToken cancellationToken);
}

public class SecretsService(IAmazonSecretsManager secretsManager, ILogger<SecretsService> logger) : ISecretsService
{
    public async Task<string?> PagerDutyIntegrationKey(string team, CancellationToken cancellationToken)
    {
        const string secretType = "integration-keys";
        return await GetSecret(team, secretType, cancellationToken);
    }

    private async Task<string?> GetSecret(string team, string secretType, CancellationToken cancellationToken)
    {
        if (secretsManager.Config.RegionEndpoint != null)
        {
            logger.LogInformation("SecretsManager client configured for region: {Region}",
                secretsManager.Config.RegionEndpoint.SystemName);
        }

        var secretId = "cdp/notify/backend/" + secretType;
        var request = new GetSecretValueRequest { SecretId = secretId };

        try
        {
            var response = await secretsManager.GetSecretValueAsync(request, cancellationToken);
            var secretString = response.SecretString ??
                               Encoding.UTF8.GetString(response.SecretBinary.ToArray());

            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(secretString);

            if (secrets is null || !secrets.TryGetValue(team, out var teamKey))
            {
                logger.LogWarning("No secret found for team: {Team}", team);
                return null;
            }

            return teamKey;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving secret ({SecretId}): {Message}", secretId, ex.Message);
            return null;
        }
    }

}