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
    /// <returns>The password value retrieved from Akeyless.</returns>
    /// <exception cref="Exception">Thrown when required parameters are missing or invalid.</exception>
    /// <exception cref="InvalidTokenException">Thrown when authentication with Akeyless fails.</exception>
    /// <exception cref="HttpRequestException">Thrown when communication with Akeyless fails.</exception>
    public string GetPassword(Dictionary<string, string> instanceParameters,
        Dictionary<string, string> serverConfigurationParameters)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Akeyless PAM Provider invoked");
            // NOTE: serverConfigurationParameters intentionally not logged — contains AccessId/AccessKey.
            Logger.LogTrace("instanceParameters: {@InstanceParameters}", instanceParameters);

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
                    Logger.LogDebug("Authenticating with Akeyless using access_key auth, AccessId: '{AccessId}'",
                        configurationInfo.AccessId);
                    var token = client.Authenticate(configurationInfo.AccessId, configurationInfo.AccessKey);

                    if (string.IsNullOrEmpty(token))
                    {
                        Logger.LogError(
                            "Authentication failed: unable to obtain access token from Akeyless for AccessId '{AccessId}'",
                            configurationInfo.AccessId);
                        throw new InvalidTokenException("Unable to obtain access token from Akeyless server");
                    }

                    AuthToken = token;
                    Logger.LogInformation(
                        "Successfully authenticated with Akeyless using AccessId '{AccessId}'",
                        configurationInfo.AccessId);
                    break;

                default:
                    Logger.LogWarning(
                        "No authentication performed for unrecognised auth type '{AuthType}'",
                        configurationInfo.AuthType);
                    break;
            }

            return client;
        }
        catch (ApiException ex)
        {
            Logger.LogError(ex, "Akeyless API exception during authentication");
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
                Logger.LogDebug("No field name specified; returning full JSON blob");
                return secretValueStr;
            }

            if (jsonObj != null && jsonObj.TryGetValue(fieldName, out var fieldValue))
            {
                Logger.LogDebug("Successfully extracted field '{FieldName}' from JSON secret", fieldName);
                return fieldValue.ToString() ?? string.Empty;
            }

            Logger.LogError("JSON secret does not contain the specified field '{FieldName}'", fieldName);
            throw new InvalidSecretConfigurationException(
                $"Secret does not contain the specified field '{fieldName}'");
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to parse secret value as JSON");
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
            var lineIndex = 0;
            foreach (var line in secretValueStr.Split('\n'))
            {
                lineIndex++;
                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    Logger.LogWarning("Skipping malformed KV entry at line {LineIndex}", lineIndex);
                    continue;
                }

                var k = parts[0].Trim();
                // NOTE: value is intentionally not logged to prevent secret exposure.
                Logger.LogDebug("Evaluating KV key '{Key}' at line {LineIndex}", k, lineIndex);
                if (k != fieldName) continue;

                Logger.LogDebug("Successfully extracted field '{FieldName}' from KV secret", fieldName);
                return parts[1].Trim();
            }

            Logger.LogError("KV secret does not contain the specified field '{FieldName}'", fieldName);
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
            Logger.LogDebug("Fetching secret '{SecretName}' (type: {SecretType}) from Akeyless",
                configurationInfo.SecretName, configurationInfo.SecretType);

            var secrets = await client.GetSecretValuesAsync([configurationInfo.SecretName], AuthToken);

            if (!secrets.TryGetValue(configurationInfo.SecretName, out var secretValueStr))
            {
                Logger.LogError("Secret '{SecretName}' was not found in Akeyless",
                    configurationInfo.SecretName);
                throw new InvalidSecretConfigurationException(
                    $"Secret '{configurationInfo.SecretName}' not found in Akeyless");
            }

            if (string.IsNullOrEmpty(secretValueStr))
            {
                Logger.LogError("Secret '{SecretName}' exists in Akeyless but has an empty value",
                    configurationInfo.SecretName);
                throw new InvalidSecretConfigurationException(
                    $"Secret '{configurationInfo.SecretName}' is empty");
            }

            string result;
            if (LooksLikeJson(secretValueStr))
            {
                Logger.LogDebug("Secret '{SecretName}' value is JSON-formatted", configurationInfo.SecretName);
                result = configurationInfo.SecretType is "static_json" or "static_kv"
                    ? ParseJsonSecret(secretValueStr, configurationInfo.StaticSecretFieldName)
                    : secretValueStr;
            }
            else if (secretValueStr.Contains('=') && secretValueStr.Contains('\n'))
            {
                Logger.LogDebug("Secret '{SecretName}' value is KV-formatted", configurationInfo.SecretName);
                result = ParseKvSecret(secretValueStr, configurationInfo.StaticSecretFieldName);
            }
            else
            {
                Logger.LogDebug("Secret '{SecretName}' value is plain text", configurationInfo.SecretName);
                result = secretValueStr;
            }

            Logger.LogInformation(
                "Successfully retrieved secret '{SecretName}' (type: {SecretType}) from Akeyless",
                configurationInfo.SecretName, configurationInfo.SecretType);
            return result;
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
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to Akeyless fails.</exception>
    /// <exception cref="Exception">Thrown when deserializing the response fails or the requested secret is not found.</exception>
    private async Task<string> GetAkeylessSecretAsync(AkeylessConfiguration configurationInfo)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Connecting to Akeyless at '{Url}'", configurationInfo.Url);
            var client = InitClient(configurationInfo);

            switch (configurationInfo.SecretType)
            {
                case "static_text":
                case "static_kv":
                case "static_json":
                    return await GetStaticSecret(client, configurationInfo);
                default:
                    Logger.LogError("Unsupported secret type '{SecretType}' — valid types are: {ValidTypes}",
                        configurationInfo.SecretType,
                        string.Join(", ", AkeylessConfiguration.SupportedSecretTypes));
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
            Logger.LogDebug("Validating server configuration parameters for auth type '{AuthType}'", authType);

            switch (authType)
            {
                case "implicit":
                    Logger.LogWarning("No credential validation performed for 'implicit' auth type");
                    break;

                case "access_key":
                    Logger.LogDebug("Validating access_key credentials");
                    ValidateAuthTypeAccessKey(connectionConfiguration);
                    break;
                default:
                    Logger.LogError("Unsupported auth type '{AuthType}' specified in server configuration", authType);
                    Logger.MethodExit();
                    throw new Exception(
                        $"Invalid auth type '{authType}' specified.");
            }

            Logger.LogDebug("Server configuration parameters are valid");
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
            Logger.LogDebug("Validating required parameter '{ParamName}'", paramName);

            if (config.ContainsKey(paramName) && !string.IsNullOrEmpty(config[paramName])) return;
            Logger.LogError("{ErrorPrefix} '{ParamName}' is required but was not provided", errorPrefix, paramName);
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
    /// <param name="connectionConfiguration">Dictionary containing connection and authentication parameters for Akeyless.</param>
    /// <returns>A fully populated AkeylessConfiguration object.</returns>
    /// <exception cref="Exception">Thrown when required parameters are missing or invalid.</exception>
    private AkeylessConfiguration BuildAkeylessConfiguration(
        IReadOnlyDictionary<string, string> instanceParameters,
        IReadOnlyDictionary<string, string> connectionConfiguration)
    {
        try
        {
            Logger.MethodEntry();
            Logger.LogDebug("Building and validating Akeyless configuration");
            var validServer = ValidateServerConfigurationParams(connectionConfiguration);
            var validInstance = ValidateInstanceParams(instanceParameters);

            if (!validServer || !validInstance)
            {
                Logger.LogError("Akeyless PAM provider configuration is invalid; see preceding log entries for details");
                throw new InvalidClientConfigurationException(
                    "Akeyless configuration is invalid, please review server logs.");
            }

            if (!connectionConfiguration.TryGetValue(AkeylessConfiguration.AUTH_TYPE, out var authType))
            {
                Logger.LogWarning(
                    "'{AuthType}' parameter not provided; defaulting to 'access_key'",
                    AkeylessConfiguration.AUTH_TYPE);
                authType = "access_key";
            }

            var config = new AkeylessConfiguration
            {
                Url = connectionConfiguration.GetValueOrDefault(AkeylessConfiguration.AKEYLESS_API_URL,
                    AkeylessConstants.DefaultAkeylessApiUrl),
                AuthType = authType
            };
            Logger.LogDebug("Using Akeyless URL '{Url}', auth type '{AuthType}'", config.Url, config.AuthType);

            switch (authType)
            {
                case "implicit":
                    Logger.LogDebug("Implicit auth type configured; credentials expected via environment variables");
                    break;
                case "access_key":
                    config.AccessId = connectionConfiguration[AkeylessConfiguration.ACCESS_ID];
                    config.AccessKey = connectionConfiguration[AkeylessConfiguration.ACCESS_KEY];
                    // NOTE: AccessId logged (not secret), AccessKey intentionally omitted.
                    Logger.LogDebug("Access key auth configured with AccessId '{AccessId}'", config.AccessId);
                    break;
                default:
                    Logger.LogError("Unsupported auth type '{AuthType}' encountered during configuration build", authType);
                    throw new Exception($"Invalid grant type '{authType}' specified");
            }

            config.SecretType = instanceParameters.GetValueOrDefault(AkeylessConfiguration.SECRET_TYPE, "");
            config.SecretName = instanceParameters[AkeylessConfiguration.SECRET_NAME];
            Logger.LogDebug("Configured to retrieve secret '{SecretName}' (type: '{SecretType}')",
                config.SecretName, config.SecretType);

            switch (config.SecretType)
            {
                case "static_kv":
                    config.StaticSecretFieldName = instanceParameters[AkeylessConfiguration.STATIC_SECRET_FIELD_NAME];
                    Logger.LogDebug("KV field name set to '{FieldName}'", config.StaticSecretFieldName);
                    break;
                case "static_json":
                    config.StaticSecretFieldName = instanceParameters.GetValueOrDefault(
                        AkeylessConfiguration.STATIC_SECRET_FIELD_NAME, "");
                    if (!string.IsNullOrEmpty(config.StaticSecretFieldName))
                        Logger.LogDebug("JSON field name set to '{FieldName}'", config.StaticSecretFieldName);
                    else
                        Logger.LogDebug("No JSON field name specified; full JSON blob will be returned");
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

            Logger.LogDebug("Akeyless configuration built successfully");
            return config;
        }
        finally
        {
            Logger.MethodExit();
        }
    }
}
