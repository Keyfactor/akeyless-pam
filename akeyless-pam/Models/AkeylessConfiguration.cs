// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;

namespace Keyfactor.Extensions.Pam.Akeyless.Models;

/// <summary>
///     Configuration class for connecting to and retrieving secrets from Akeyless.
/// </summary>
internal class AkeylessConfiguration : IValidatableObject
{
    public static readonly ImmutableList<string> SupportedAuthMethods =
        ImmutableList.Create(AkeylessConstants.DefaultAuthMethod);

    public static readonly ImmutableList<string> SupportedSecretTypes = ImmutableList.Create(
        "static_text", "static_json", "static_kv"
    );

    public static readonly ImmutableList<string> UnsupportedAuthMethods = ImmutableList.Create(
        "saml"
    );

    /// <summary>
    ///     Initializes a new instance of the <see cref="AkeylessConfiguration" /> class with empty strings.
    /// </summary>
    /// <remarks>
    ///     This constructor initializes required string properties with empty strings to satisfy nullable requirements.
    ///     Validation logic in the Validate method ensures actual values are provided when needed.
    /// </remarks>
    public AkeylessConfiguration()
    {
        // Initialize non-nullable string properties to satisfy compiler
        Url = string.Empty;
        AccessId = string.Empty;
        AccessKey = string.Empty;
        StaticSecretFieldName = string.Empty;
        AuthType = "access_key"; // Default value is already set in property declaration
    }

    /// <summary>
    ///     The configuration for the AKeyless API URL.
    /// </summary>
    public static string AKEYLESS_API_URL => "Url";

    /// <summary>
    ///     The configuration key for the auth type used for authentication. For more information on auth_types see
    ///     https://docs.akeyless.io/reference/auth `access-type`.
    /// </summary>
    /// <remarks>
    ///     Supported auth types:
    ///     - `access_key`: Uses AccessId and AccessKey for authentication.
    /// </remarks>
    public static string AUTH_TYPE => "AuthType";

    /// <summary>
    ///     The configuration key for the access ID used in access key authentication.
    /// </summary>
    public static string ACCESS_ID => "AccessId";

    /// <summary>
    ///     The configuration key for the access key used in access key authentication.
    /// </summary>
    public static string ACCESS_KEY => "AccessKey";

    /// <summary>
    ///     The configuration key for the type of secret to retrieve from Akeyless. For more information on secret types see
    ///     https://docs.akeyless.io/docs/manage-your-secrets-overview
    /// </summary>
    /// <remarks>
    ///     Supported secret types:
    ///     - `static_text`: A static text secret.
    ///     - `static_json`: A static JSON secret.
    ///     - `static_kv`: A static key-value secret.
    /// </remarks>
    public static string SECRET_TYPE => "SecretType";

    /// <summary>
    ///     The configuration key for the name of the secret to retrieve from Akeyless.
    /// </summary>
    public static string SECRET_NAME => "SecretName";

    /// <summary>
    ///     The configuration key for the field name within the secret to retrieve.
    /// </summary>
    public static string STATIC_SECRET_FIELD_NAME => "StaticSecretFieldName";

    /// <summary>
    ///     The base URL of the Akeyless Secret Server.
    /// </summary>
    /// <remarks>
    ///     Defaults to the public Akeyless API URL if not specified.
    /// </remarks>
    public string Url { get; init; }

    /// <summary>
    ///     The access ID for access_key authentication with Akeyless.
    ///     For more information see https://docs.akeyless.io/docs/api-key
    /// </summary>
    public string AccessId { get; set; }

    /// <summary>
    ///     The access key for access_key authentication with Akeyless.
    ///     For more information see https://docs.akeyless.io/docs/api-key
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    ///     The type of the secret to retrieve from Akeyless.
    /// </summary>
    /// <remarks>
    ///     Must be one of:
    ///     - `static_text`
    ///     - `static_json`
    ///     - `static_kv`.
    ///     Defaults to `static_text`.
    /// </remarks>
    [Required(ErrorMessage = "The SecretType field is required.")]
    [RegularExpression("^(static_text|static_json|static_kv)$",
        ErrorMessage = "SecretType must be one of `[static_text,static_json,static_kv]`.")]
    public string SecretType { get; set; } = "static_text";

    /// <summary>
    ///     The identifier of the secret to retrieve from Akeyless.
    /// </summary>
    [Required(ErrorMessage = "The SecretName field is required.")]
    public string SecretName { get; set; }

    /// <summary>
    ///     The name of the field within the secret to retrieve.
    ///     This can be either the field name or slug.
    /// </summary>
    public string StaticSecretFieldName { get; set; }

    /// <summary>
    ///     The auth type to use for authentication to AKeyless.
    /// </summary>
    /// <remarks>
    ///     Defaults to `access_key`.
    ///     Unsupported auth types:
    ///     - `saml`: Due to requiring a web browser for SAML assertions.
    /// </remarks>
    [RegularExpression(
        "^(access_key|password|ldap|k8s|azure_ad|oidc|aws_iam|universal_identity|jwt|gcp|cert|oci|kerberos)$",
        ErrorMessage =
            "AuthType must be one of `[access_key,password,ldap,k8s,azure_ad,oidc,aws_iam,universal_identity,jwt,gcp,cert,oci,kerberos]`.")]
    public string AuthType { get; init; }

    /// <summary>
    ///     Validates that the configuration has either username/password or client credentials for authentication.
    /// </summary>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A collection of validation results.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (AuthType)
        {
            case AkeylessConstants.DefaultAuthMethod:
                if (string.IsNullOrWhiteSpace(AccessId) || string.IsNullOrWhiteSpace(AccessKey))
                    yield return new ValidationResult(
                        $"AccessId and AccessKey must be provided for '{AkeylessConstants.DefaultAuthMethod}' authentication.",
                        [nameof(AccessId), nameof(AccessKey)]);
                break;
            default:
                yield return new ValidationResult(
                    $"Unsupported AuthType. Currently, only '{string.Join(", ", SupportedAuthMethods)}' are supported.",
                    [nameof(AuthType)]);
                break;
        }

        if (!SupportedSecretTypes.Contains(SecretType))
            yield return new ValidationResult(
                $"Unsupported SecretType. Supported types are: {string.Join(", ", SupportedSecretTypes)}.",
                [nameof(SecretType)]
            );
        // StaticSecretFieldName is required for static_kv, optional for static_json
        if (SecretType == "static_kv" && string.IsNullOrWhiteSpace(StaticSecretFieldName))
            yield return new ValidationResult(
                "StaticSecretFieldName must be provided when SecretType is 'static_kv'.",
                [nameof(StaticSecretFieldName)]
            );
    }
}