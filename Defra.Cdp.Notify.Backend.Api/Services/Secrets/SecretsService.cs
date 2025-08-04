using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace Defra.Cdp.Notify.Backend.Api.Services.Secrets;

public interface ISecretsService
{
    Task<string> PagerDutyIntegrationKey(string team, CancellationToken cancellationToken);
}

public class SecretsService(IAmazonSecretsManager secretsManager, ILogger<SecretsService> logger) : ISecretsService
{
    public async Task<string> PagerDutyIntegrationKey(string team, CancellationToken cancellationToken)
    {
        const string secretType = "integration-keys";
        return await GetSecret(team, secretType, cancellationToken);
    }

    private async Task<string> GetSecret(string team, string secretType, CancellationToken cancellationToken)
    {
        if (secretsManager.Config.RegionEndpoint != null)
        {
            logger.LogInformation("SecretsManager client configured for region: {Region}",
                secretsManager.Config.RegionEndpoint.SystemName);
        }

        var secretId = "cdp/notify/backend/" + secretType + "/" + team;
        var request = new GetSecretValueRequest { SecretId = secretId };

        try
        {
            var response = await secretsManager.GetSecretValueAsync(request, cancellationToken);

            return response.SecretString ??
                   System.Text.Encoding.UTF8.GetString(response.SecretBinary.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving secret ({SecretId}): {Message}", secretId, ex.Message);
            throw;
        }
    }

}