using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;

namespace ProcessadorDiagramas.ProcessingService.API;

internal static class AwsRuntimeConfigurationBootstrapper
{
    public static void AddAwsRuntimeSecrets(IConfigurationBuilder configurationBuilder)
    {
        var currentConfiguration = configurationBuilder.Build();
        var region = currentConfiguration["Aws:Region"];

        if (string.IsNullOrWhiteSpace(region))
            return;

        var overrides = new Dictionary<string, string?>();
        var serviceUrl = currentConfiguration["Aws:ServiceURL"];

        if (string.IsNullOrWhiteSpace(currentConfiguration.GetConnectionString("DefaultConnection")))
        {
            var parameterArn = currentConfiguration["Aws:DbConnectionParameterArn"];
            if (!string.IsNullOrWhiteSpace(parameterArn))
            {
                overrides["ConnectionStrings:DefaultConnection"] = GetParameterValue(parameterArn, region, serviceUrl);
            }
        }

        if (string.IsNullOrWhiteSpace(currentConfiguration["AiProvider:ApiKey"]))
        {
            var secretArn = currentConfiguration["Aws:AiApiKeySecretArn"];
            if (!string.IsNullOrWhiteSpace(secretArn))
            {
                overrides["AiProvider:ApiKey"] = GetSecretValue(secretArn, region, serviceUrl);
            }
        }

        if (overrides.Count > 0)
            configurationBuilder.AddInMemoryCollection(overrides);
    }

    private static string GetParameterValue(string parameterArn, string region, string? serviceUrl)
    {
        using var client = CreateSsmClient(region, serviceUrl);
        var response = client.GetParameterAsync(new GetParameterRequest
        {
            Name = parameterArn,
            WithDecryption = true
        }).GetAwaiter().GetResult();

        return response.Parameter.Value;
    }

    private static string GetSecretValue(string secretArn, string region, string? serviceUrl)
    {
        using var client = CreateSecretsClient(region, serviceUrl);
        var response = client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secretArn
        }).GetAwaiter().GetResult();

        return ExtractSecretString(response.SecretString);
    }

    private static string ExtractSecretString(string? secretString)
    {
        if (string.IsNullOrWhiteSpace(secretString))
            throw new InvalidOperationException("SecretString is empty.");

        var trimmed = secretString.Trim();
        if (!trimmed.StartsWith('{'))
            return trimmed;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(trimmed);
            var root = document.RootElement;

            if (TryReadJsonProperty(root, "apiKey", out var apiKey)
                || TryReadJsonProperty(root, "ApiKey", out apiKey)
                || TryReadJsonProperty(root, "value", out apiKey)
                || TryReadJsonProperty(root, "Value", out apiKey)
                || TryReadJsonProperty(root, "key", out apiKey)
                || TryReadJsonProperty(root, "Key", out apiKey))
            {
                return apiKey!;
            }
        }
        catch
        {
        }

        return trimmed;
    }

    private static bool TryReadJsonProperty(System.Text.Json.JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private static IAmazonSimpleSystemsManagement CreateSsmClient(string region, string? serviceUrl)
    {
        var credentials = CreateCredentials();

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            return new AmazonSimpleSystemsManagementClient(credentials, new AmazonSimpleSystemsManagementConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = region,
                UseHttp = serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            });
        }

        return new AmazonSimpleSystemsManagementClient(credentials, RegionEndpoint.GetBySystemName(region));
    }

    private static IAmazonSecretsManager CreateSecretsClient(string region, string? serviceUrl)
    {
        var credentials = CreateCredentials();

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            return new AmazonSecretsManagerClient(credentials, new AmazonSecretsManagerConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = region,
                UseHttp = serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            });
        }

        return new AmazonSecretsManagerClient(credentials, RegionEndpoint.GetBySystemName(region));
    }

    private static BasicAWSCredentials CreateCredentials()
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";

        return new BasicAWSCredentials(accessKey, secretKey);
    }
}