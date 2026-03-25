// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using Keyfactor.Extensions.Pam.Akeyless;
using Xunit;

namespace Keyfactor.Tests.Integration;

/// <summary>
///     Integration tests that connect to a live Akeyless instance.
///     All tests are skipped when the required environment variables are not set.
/// </summary>
/// <remarks>
///     Required env vars for all tests:
///       AKEYLESS_ACCESS_ID       — Akeyless access key ID
///       AKEYLESS_ACCESS_KEY      — Akeyless access key secret
///
///     Optional env vars:
///       AKEYLESS_API_URL         — Akeyless API URL (defaults to https://api.akeyless.io)
///       AKEYLESS_AUTH_TYPE       — Auth type (defaults to access_key)
///
///     Per-test secret path env vars:
///       AKEYLESS_SECRET_STATIC_TEXT      — path to a static_text secret
///       AKEYLESS_SECRET_STATIC_KV        — path to a static_kv secret with "username" and "password" fields
///       AKEYLESS_SECRET_STATIC_JSON      — path to a static_json secret with "username" and "password" fields
///       AKEYLESS_SECRET_STATIC_JSON_RAW  — path to a static_json secret to retrieve as a raw blob
/// </remarks>
public class AkeylessPamIntegrationTests
{
    private static Dictionary<string, string> BuildServerParams()
    {
        return new Dictionary<string, string>
        {
            ["Url"] = Env("AKEYLESS_API_URL", "https://api.akeyless.io"),
            ["AuthType"] = Env("AKEYLESS_AUTH_TYPE", "access_key"),
            ["AccessId"] = Env("AKEYLESS_ACCESS_ID"),
            ["AccessKey"] = Env("AKEYLESS_ACCESS_KEY")
        };
    }

    private static string Env(string key, string? fallback = null)
        => Environment.GetEnvironmentVariable(key) ?? fallback ?? string.Empty;

    private static void SkipIfMissingCredentials()
    {
        var id = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_ID");
        var key = Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_KEY");
        Skip.If(string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key),
            "AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY env vars not set; skipping integration tests.");
    }

    private static void SkipIfMissingSecretPath(string envVar)
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)),
            $"{envVar} env var not set; skipping this integration test.");
    }

    [SkippableFact]
    public void GetPassword_StaticText_ReturnsNonEmptyValue()
    {
        SkipIfMissingCredentials();
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_TEXT");

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_text",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_TEXT")
        };

        var result = pam.GetPassword(instance, BuildServerParams());

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [SkippableFact]
    public void GetPassword_StaticKv_UsernameField_ReturnsValue()
    {
        SkipIfMissingCredentials();
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_KV");

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_kv",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_KV"),
            ["StaticSecretFieldName"] = "username"
        };

        var result = pam.GetPassword(instance, BuildServerParams());

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [SkippableFact]
    public void GetPassword_StaticKv_PasswordField_ReturnsValue()
    {
        SkipIfMissingCredentials();
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_KV");

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_kv",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_KV"),
            ["StaticSecretFieldName"] = "password"
        };

        var result = pam.GetPassword(instance, BuildServerParams());

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [SkippableFact]
    public void GetPassword_StaticJson_UsernameField_ReturnsValue()
    {
        SkipIfMissingCredentials();
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_JSON");

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_json",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_JSON"),
            ["StaticSecretFieldName"] = "username"
        };

        var result = pam.GetPassword(instance, BuildServerParams());

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [SkippableFact]
    public void GetPassword_StaticJson_PasswordField_ReturnsValue()
    {
        SkipIfMissingCredentials();
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_JSON");

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_json",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_JSON"),
            ["StaticSecretFieldName"] = "password"
        };

        var result = pam.GetPassword(instance, BuildServerParams());

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [SkippableFact]
    public void GetPassword_StaticJson_NoFieldName_ReturnsRawJsonBlob()
    {
        SkipIfMissingCredentials();
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_JSON_RAW");

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_json",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_JSON_RAW")
            // no StaticSecretFieldName — expect full JSON blob back
        };

        var result = pam.GetPassword(instance, BuildServerParams());

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(result.TrimStart().StartsWith('{') || result.TrimStart().StartsWith('['),
            "Expected raw JSON blob but got: " + result);
    }

    [SkippableFact]
    public void GetPassword_BadCredentials_ThrowsInvalidClientConfigurationException()
    {
        SkipIfMissingSecretPath("AKEYLESS_SECRET_STATIC_TEXT"); // need a valid secret path

        var server = new Dictionary<string, string>
        {
            ["Url"] = Env("AKEYLESS_API_URL", "https://api.akeyless.io"),
            ["AuthType"] = "access_key",
            ["AccessId"] = "p-bad-id",
            ["AccessKey"] = "bad-key"
        };
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_text",
            ["SecretName"] = Env("AKEYLESS_SECRET_STATIC_TEXT")
        };

        var pam = new AkeylessPam();
        var ex = Assert.Throws<AggregateException>(() => pam.GetPassword(instance, server));
        Assert.IsType<InvalidClientConfigurationException>(ex.InnerException);
    }

    [SkippableFact]
    public void GetPassword_NonexistentSecret_ThrowsException()
    {
        SkipIfMissingCredentials();

        var pam = new AkeylessPam();
        var instance = new Dictionary<string, string>
        {
            ["SecretType"] = "static_text",
            ["SecretName"] = "/pam/does/not/exist/at/all"
        };

        // Akeyless returns an ApiException (404-like) for nonexistent secrets rather than
        // an empty response, so it propagates as-is. Both domain exceptions and ApiException
        // are acceptable here until the adapter normalizes SDK errors into domain exceptions.
        Assert.ThrowsAny<Exception>(() => pam.GetPassword(instance, BuildServerParams()));
    }

    [SkippableFact]
    public void GetPassword_StaticJson_NoFieldName_ReturnsRawJsonBlob_K8sOrchestratorSecret()
    {
        SkipIfMissingCredentials();
        const string secretName = "/pam/test/k8s-orchestrator";

        var pam = new AkeylessPam();
        var result = pam.GetPassword(
            new Dictionary<string, string> { ["SecretType"] = "static_json", ["SecretName"] = secretName },
            BuildServerParams());

        Assert.NotEmpty(result);
        Assert.True(result.TrimStart().StartsWith('{') || result.TrimStart().StartsWith('['),
            "Expected raw JSON blob");
    }

    [SkippableFact]
    public void GetPassword_StaticJson_WhitespaceFieldName_ReturnsRawJsonBlob_K8sOrchestratorSecret()
    {
        SkipIfMissingCredentials();
        const string secretName = "/pam/test/k8s-orchestrator";

        var pam = new AkeylessPam();
        var result = pam.GetPassword(
            new Dictionary<string, string> { ["SecretType"] = "static_json", ["SecretName"] = secretName, ["StaticSecretFieldName"] = "   " },
            BuildServerParams());

        Assert.NotEmpty(result);
        Assert.True(result.TrimStart().StartsWith('{') || result.TrimStart().StartsWith('['),
            "Expected raw JSON blob even when StaticSecretFieldName is whitespace-only");
    }
}
