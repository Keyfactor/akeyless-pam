// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using akeyless.Client;
using Keyfactor.Extensions.Pam.Akeyless.Models;
using Keyfactor.Logging;
using Keyfactor.Platform.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Pam.Akeyless;

/// <summary>
///     Exception thrown when the authentication token for Akeyless is invalid or cannot be obtained.
/// </summary>
/// <remarks>
///     This exception is typically thrown when authentication credentials are incorrect or the server rejects the auth
///     request.
/// </remarks>
public class InvalidTokenException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidTokenException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidTokenException(string message) : base(message)
    {
    }
}

public class InvalidClientConfigurationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidClientConfigurationException" /> class with a specified error
    ///     message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidClientConfigurationException(string message) : base(message)
    {
    }
}

public class InvalidSecretConfigurationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidSecretConfigurationException" /> class with a specified error
    ///     message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidSecretConfigurationException(string message) : base(message)
    {
    }
}

/// <summary>
///     Privileged Access Management (PAM) provider implementation for Akeyless.
/// </summary>
/// <remarks>
///     This class implements the IPAMProvider interface to retrieve secrets from Akeyless.
/// </remarks>
public class AkeylessPam : IPAMProvider
{
    private readonly Func<string, IAkeylessApiClient> _clientFactory;

    private ILogger Logger { get; } = LogHandler.GetClassLogger<AkeylessPam>();

    private string AuthToken { get; set; } = string.Empty;

    public AkeylessPam() : this(basePath => new AkeylessApiClient(basePath))
    {
    }

    internal AkeylessPam(Func<string, IAkeylessApiClient> clientFactory)
    {
        _clientFactory = clientFactory;
    }

    /// <summary>
    ///     Gets the name of this PAM provider.
    /// </summary>
    /// <value>The string "Akeyless".</value>
    public string Name => "Akeyless";

    /// <summary>
    ///     Retrieves a password from Akeyless using the provided configuration parameters.
    /// </summary>
    /// <param name="instanceParameters">Dictionary containing instance-specific parameters like SecretId and SecretFieldName.</param>
    /// <param name="serverConfigurationParameters">
    ///     Dictionary containing connection and authentication parameters such as host URL,
    ///     username, and password.
    /// </param>
    /// <returns>The password value retrieved from.</returns>
    /// <exception cref="Exception">Thrown when required parameters are missing or invalid.</exception>
    /// <exception cref="InvalidTokenException">Thrown when authentication with fails.</exception>
    /// <exception cref="HttpRequestException">Thrown when communication with fails.</exception>
    public string GetPassword(Dictionary<string, string> instanceParameters,
        Dictionary<string, string> serverConfigurationParameters)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogInformation("Starting Akeyless PAM Provider");
            Logger.LogDebug("Getting password from Akeyless");
            Logger.LogTrace("instanceParameters: {@InstanceParameters}", instanceParameters);
            // Logger.LogTrace("initializationInfo: {@ServerConfigurationParameters}",
            //     serverConfigurationParameters); // TODO: Commented out to avoid logging sensitive information

            var config = BuildAkeylessConfiguration(instanceParameters, serverConfigurationParameters);
            return GetAkeylessSecretAsync(config).Result;
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    private IAkeylessApiClient InitClient(AkeylessConfiguration configurationInfo)
    {
        try
        {
            Logger.MethodEntry();
            var basePath = Environment.GetEnvironmentVariable("AKEYLESS_API_URL") ??
                           configurationInfo.Url ?? "https://api.akeyless.io";

            var client = _clientFactory(basePath);

            switch (configurationInfo.AuthType)
            {
                case "access_key":
                    Logger.LogDebug("Authenticating with Akeyless using access_key");
                    var token = client.Authenticate(configurationInfo.AccessId, configurationInfo.AccessKey);

                    if (string.IsNullOrEmpty(token))
                    {
                        Logger.LogError("Unable to obtain access token from Akeyless server");
                        throw new InvalidTokenException("Unable to obtain access token from Akeyless server");
                    }

                    AuthToken = token;
                    Logger.LogInformation("Successfully authenticated with Akeyless");
                    break;

                default:
                    Logger.LogWarning("No authentication performed for auth type '{AuthType}'",
                        configurationInfo.AuthType);
                    break;
            }

            return client;
        }
        catch (ApiException ex)
        {
            Logger.LogError(ex, "Akeyless API exception during client initialization");
            throw new InvalidClientConfigurationException(
                $"Unable to authenticate to Akeyless API. {ex.Message}");
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    private static bool LooksLikeJson(string s)
    {
        s = s.Trim();
        return (s.StartsWith('{') && s.EndsWith('}')) || (s.StartsWith('[') && s.EndsWith(']'));
    }


    private string ParseJsonSecret(string secretValueStr, string fieldName = "")
    {
        try
        {
            Logger.MethodEntry();
            var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(secretValueStr);
            if (string.IsNullOrEmpty(fieldName))
            {
                Logger.LogDebug("No field name specified, returning full JSON secret");
                return secretValueStr;
            }

            if (jsonObj != null && jsonObj.TryGetValue(fieldName, out var fieldValue))
            {
                Logger.LogDebug("Returning value for field '{FieldName}'", fieldName);
                return fieldValue.ToString() ?? string.Empty;
            }

            Logger.LogError("❌ Secret does not contain the specified field '{FieldName}'", fieldName);
            throw new InvalidSecretConfigurationException(
                $"Secret does not contain the specified field '{fieldName}'");
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "❌ Failed to parse secret as JSON");
            throw;
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    private string ParseKvSecret(string secretValueStr, string fieldName)
    {
        try
        {
            Logger.MethodEntry();
            foreach (var line in secretValueStr.Split('\n'))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    Logger.LogWarning("Skipping malformed KV line: {Line}", line);
                    continue;
                }

                var k = parts[0].Trim();
                var val = parts[1].Trim();
                Logger.LogDebug("Key: {Key}, Value: {Value}", k, val);
                if (k != fieldName) continue;
                Logger.LogDebug("Returning value for field '{FieldName}'", fieldName);
                return val;
            }

            Logger.LogError("❌ Secret does not contain the specified field '{FieldName}'", fieldName);
            throw new InvalidSecretConfigurationException(
                $"Secret does not contain the specified field '{fieldName}'");
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    private async Task<string> GetStaticSecret(IAkeylessApiClient client, AkeylessConfiguration configurationInfo)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Fetching static text secret '{SecretName}'",
                configurationInfo.SecretName);

            var secrets = await client.GetSecretValuesAsync([configurationInfo.SecretName], AuthToken);

            if (!secrets.TryGetValue(configurationInfo.SecretName, out var secretValueStr))
            {
                Logger.LogError("Secret '{SecretName}' not found in Akeyless",
                    configurationInfo.SecretName);
                throw new InvalidSecretConfigurationException(
                    $"Secret '{configurationInfo.SecretName}' not found in Akeyless");
            }

            if (string.IsNullOrEmpty(secretValueStr))
            {
                Logger.LogError("Secret '{SecretName}' has an empty value",
                    configurationInfo.SecretName);
                throw new InvalidSecretConfigurationException(
                    $"Secret '{configurationInfo.SecretName}' is empty");
            }

            if (LooksLikeJson(secretValueStr))
            {
                Logger.LogInformation("✅ Secret '{SecretName}' appears to be JSON",
                    configurationInfo.SecretName);
                // Parse JSON if secret isn't meant to be a full JSON blob else returns the JSON blob
                return configurationInfo.SecretType is "static_json" or "static_kv"
                    ? ParseJsonSecret(secretValueStr, configurationInfo.StaticSecretFieldName)
                    : secretValueStr;
            }

            if (secretValueStr.Contains('=') && secretValueStr.Contains('\n'))
            {
                Logger.LogInformation("✅ Secret '{SecretName}' appears to be KV formatted",
                    configurationInfo.SecretName);
                return ParseKvSecret(secretValueStr, configurationInfo.StaticSecretFieldName);
            }


            Logger.LogInformation("✅ Secret '{SecretName}' appears to be plain text",
                configurationInfo.SecretName);
            return secretValueStr;
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    /// <summary>
    ///     Asynchronously retrieves a secret from Akeyless.
    /// </summary>
    /// <param name="configurationInfo">The configuration containing connection and request details.</param>
    /// <returns>The value of the requested secret field.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to fails.</exception>
    /// <exception cref="Exception">Thrown when deserializing the response fails or the requested secret is not found.</exception>
    private async Task<string> GetAkeylessSecretAsync(AkeylessConfiguration configurationInfo)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Attempting to fetch access token from Akeyless at {Url}",
                configurationInfo.Url);
            var client = InitClient(configurationInfo);

            switch (configurationInfo.SecretType)
            {
                case "static_text":
                case "static_kv":
                case "static_json":
                    return await GetStaticSecret(client, configurationInfo);
                default:
                    Logger.LogError("Invalid or unsupported secret type '{SecretType}' specified",
                        configurationInfo.SecretType);
                    throw new Exception(
                        $"Invalid secret type '{configurationInfo.SecretType}' specified, please use one of [{string.Join(", ", AkeylessConfiguration.SupportedSecretTypes)}]");
            }
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    /// <summary>
    ///     Validates the instance parameters provided to the PAM provider.
    /// </summary>
    /// <param name="instanceParameters">
    ///     A read-only dictionary containing instance-specific parameters, such as SecretId and SecretFieldName.
    /// </param>
    /// <returns>
    ///     True if the instance parameters are valid; otherwise, throws an <see cref="InvalidSecretConfigurationException" />.
    /// </returns>
    /// <exception cref="InvalidSecretConfigurationException">
    ///     Thrown if required parameters are missing or cannot be parsed as expected.
    /// </exception>
    private bool ValidateInstanceParams(IReadOnlyDictionary<string, string> instanceParameters)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Validating instance parameters");
            ValidateRequiredParameter(instanceParameters, AkeylessConfiguration.SECRET_NAME,
                "instance configuration parameter");
            Logger.LogDebug("Instance parameters are valid");
            return true;
        }
        catch (MissingFieldException ex)
        {
            Logger.LogError(ex, "Instance parameter validation failed");
            return false;
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    /// <summary>
    ///     Validates the server configuration parameters for connecting to Akeyless.
    /// </summary>
    /// <param name="connectionConfiguration">
    ///     A read-only dictionary containing server configuration parameters such as URL, credentials, and grant
    ///     type.
    /// </param>
    /// <param name="authType">
    ///     The auth type to use for interacting with the Akeyless API. Supported values are "access_key".
    ///     Defaults to "access_key".
    /// </param>
    /// <returns>
    ///     True if the server configuration parameters are valid; otherwise, throws an
    ///     <see cref="InvalidClientConfigurationException" />.
    /// </returns>
    /// <exception cref="InvalidClientConfigurationException">
    ///     Thrown if required parameters are missing or invalid for the specified grant type.
    /// </exception>
    private bool ValidateServerConfigurationParams(
        IReadOnlyDictionary<string, string> connectionConfiguration,
        string authType = AkeylessConstants.DefaultAuthMethod)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Validating server configuration parameters");

            // Validate credentials based on grant type
            switch (authType)
            {
                case "implicit":
                    Logger.LogWarning("No validation performed for 'implicit' auth type");
                    break;

                case "access_key":
                    Logger.LogDebug("Validating credentials for 'access_key' auth type");
                    ValidateAuthTypeAccessKey(connectionConfiguration);
                    break;
                default:
                    Logger.LogError(
                        "Invalid auth type '{AuthType}'",
                        authType);
                    Logger.MethodExit();
                    throw new Exception(
                        $"Invalid auth type '{authType}' specified.");
            }

            Logger.LogInformation("Server configuration parameters are valid");
            return true;
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    /// <summary>
    ///     Validates that a required parameter exists and is not null or empty in the provided configuration dictionary.
    /// </summary>
    /// <param name="config">
    ///     The configuration dictionary to validate.
    /// </param>
    /// <param name="paramName">
    ///     The name of the parameter to check for existence and non-empty value.
    /// </param>
    /// <param name="errorPrefix">
    ///     A string prefix to include in the error message if validation fails.
    /// </param>
    /// <exception cref="InvalidClientConfigurationException">
    ///     Thrown if the required parameter is missing or its value is null or empty.
    /// </exception>
    private void ValidateRequiredParameter(
        IReadOnlyDictionary<string, string> config,
        string paramName,
        string errorPrefix)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Validating parameter '{ParamName}'", paramName);

            if (config.ContainsKey(paramName) && !string.IsNullOrEmpty(config[paramName])) return;
            Logger.LogError("{ErrorPrefix} '{ParamName}' not provided", errorPrefix, paramName);
            throw new MissingFieldException($"{errorPrefix} '{paramName}' not provided");
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    /// <summary>
    ///     Validates that the required client ID and client secret parameters exist and are not empty for the client
    ///     credentials grant type.
    /// </summary>
    /// <param name="config">
    ///     The configuration dictionary containing client parameters.
    /// </param>
    /// <exception cref="InvalidClientConfigurationException">
    ///     Thrown if the client ID or client secret parameter is missing or empty.
    /// </exception>
    private void ValidateAuthTypeAccessKey(IReadOnlyDictionary<string, string> config)
    {
        try
        {
            Logger.MethodEntry();
            ValidateRequiredParameter(config, AkeylessConfiguration.ACCESS_KEY, "client configuration parameter");
            ValidateRequiredParameter(config, AkeylessConfiguration.ACCESS_ID, "client configuration parameter");
        }
        catch (MissingFieldException ex)
        {
            Logger.LogError(ex, "Access key authentication parameter validation failed");
            throw new InvalidClientConfigurationException(ex.Message);
        }
        finally
        {
            Logger.MethodExit();
        }
    }

    /// <summary>
    ///     Creates a AkeylessConfiguration object from the provided parameters.
    /// </summary>
    /// <param name="instanceParameters">
    ///     Dictionary containing instance-specific parameters, including the secret name and field
    ///     name.
    /// </param>
    /// <param name="connectionConfiguration">Dictionary containing connection and authentication parameters for.</param>
    /// <returns>A fully populated AkeylessConfiguration object.</returns>
    /// <exception cref="Exception">Thrown when required parameters are missing or invalid.</exception>
    private AkeylessConfiguration BuildAkeylessConfiguration(
        IReadOnlyDictionary<string, string> instanceParameters,
        IReadOnlyDictionary<string, string> connectionConfiguration)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogInformation("Validating Akeyless configuration");
            var validServer = ValidateServerConfigurationParams(connectionConfiguration);
            var validInstance = ValidateInstanceParams(instanceParameters);

            if (!validServer || !validInstance)
            {
                Logger.LogError("Akeyless PAM provider configuration is invalid");
                throw new InvalidClientConfigurationException(
                    "Akeyless configuration is invalid, please review server logs.");
            }

            if (!connectionConfiguration.TryGetValue(AkeylessConfiguration.AUTH_TYPE, out var authType))
            {
                Logger.LogWarning(
                    "\'{AuthType}\' parameter not provided defaulting to 'implicit' auth type which uses environment variables",
                    AkeylessConfiguration.AUTH_TYPE);
                authType = "access_key";
            }

            Logger.LogDebug("Building Akeyless configuration");
            var config = new AkeylessConfiguration
            {
                Url = connectionConfiguration.GetValueOrDefault(AkeylessConfiguration.AKEYLESS_API_URL,
                    AkeylessConstants.DefaultAkeylessApiUrl),
                AuthType = authType
            };
            switch (authType)
            {
                case "implicit":
                    Logger.LogInformation("Building Akeyless configuration for implicit auth type");
                    break;
                case "access_key":
                    Logger.LogInformation("Building Akeyless configuration for 'access_key' auth type");
                    config.AccessId = connectionConfiguration[AkeylessConfiguration.ACCESS_ID];
                    config.AccessKey = connectionConfiguration[AkeylessConfiguration.ACCESS_KEY];
                    break;
                default:
                    Logger.LogError("Invalid auth type '{AuthType}' specified", authType);
                    throw new Exception($"Invalid grant type '{authType}' specified");
            }

            config.SecretType = instanceParameters.GetValueOrDefault(AkeylessConfiguration.SECRET_TYPE, "");
            config.SecretName = instanceParameters[AkeylessConfiguration.SECRET_NAME];
            switch (config.SecretType)
            {
                case "static_kv":
                    Logger.LogInformation("Configuring static secret field name for secret type '{SecretType}'",
                        config.SecretType);
                    config.StaticSecretFieldName = instanceParameters[AkeylessConfiguration.STATIC_SECRET_FIELD_NAME];
                    break;
                case "static_json":
                    Logger.LogInformation("Configuring static secret field name for secret type '{SecretType}'",
                        config.SecretType);
                    config.StaticSecretFieldName = instanceParameters.GetValueOrDefault(
                        AkeylessConfiguration.STATIC_SECRET_FIELD_NAME, "");
                    break;
            }

            var validationContext = new ValidationContext(config);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(config, validationContext, validationResults, validateAllProperties: true))
            {
                var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
                Logger.LogError("Akeyless configuration model validation failed: {Errors}", errors);
                throw new InvalidClientConfigurationException(
                    $"Akeyless configuration validation failed: {errors}");
            }

            return config;
        }
        finally
        {
            Logger.MethodExit();
        }
    }
}
