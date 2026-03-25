// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using akeyless.Client;
using Keyfactor.Extensions.Pam.Akeyless;
using Xunit;

namespace Keyfactor.Tests.Integration;

/// <summary>
///     Integration tests for <see cref="AkeylessApiClient" /> exercising the real Akeyless API.
///     Credentials are loaded from environment variables or a .env file in the repo root.
///
///     Secret paths used by retrieval tests default to the same paths as TestConsole and can be
///     overridden with environment variables:
///       AKEYLESS_SECRET_STATIC_TEXT  (default: pam/test/pamStaticTextUsername)
///       AKEYLESS_SECRET_STATIC_KV    (default: pam/test/pamStaticKV)
///       AKEYLESS_SECRET_STATIC_JSON  (default: pam/test/pamStaticJSON)
/// </summary>
public class AkeylessApiClientTests
{
    static AkeylessApiClientTests() => DotEnvLoader.Load();

    private static string AccessId =>
        Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_ID") ?? string.Empty;

    private static string AccessKey =>
        Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_KEY") ?? string.Empty;

    private static string ApiUrl
    {
        get
        {
            var url = Environment.GetEnvironmentVariable("AKEYLESS_API_URL");
            return string.IsNullOrEmpty(url) ? "https://api.akeyless.io" : url;
        }
    }

    private static AkeylessApiClient Client => new(ApiUrl);

    private static void SkipIfMissingCredentials()
    {
        Skip.If(string.IsNullOrEmpty(AccessId) || string.IsNullOrEmpty(AccessKey),
            "AKEYLESS_ACCESS_ID and AKEYLESS_ACCESS_KEY not set; skipping API client integration tests.");
    }

    private static string RequireSecretPath(string envVar, string defaultPath)
    {
        var val = Environment.GetEnvironmentVariable(envVar);
        return string.IsNullOrEmpty(val) ? defaultPath : val;
    }

    // ── Authentication ──────────────────────────────────────────────────────

    [SkippableFact]
    public void Authenticate_ValidCredentials_ReturnsNonEmptyToken()
    {
        SkipIfMissingCredentials();

        var token = Client.Authenticate(AccessId, AccessKey);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [SkippableFact]
    public void Authenticate_InvalidCredentials_ThrowsApiException()
    {
        SkipIfMissingCredentials();

        Assert.Throws<ApiException>(() =>
            Client.Authenticate("p-bad-id", "bad-key"));
    }

    // ── Secret retrieval ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetSecretValuesAsync_StaticTextSecret_ReturnsDictWithValue()
    {
        SkipIfMissingCredentials();
        var secretName = RequireSecretPath("AKEYLESS_SECRET_STATIC_TEXT", "pam/test/pamStaticTextUsername");

        var client = Client;
        var token = client.Authenticate(AccessId, AccessKey);
        var result = await client.GetSecretValuesAsync([secretName], token);

        Assert.True(result.ContainsKey(secretName), $"Response did not contain key '{secretName}'");
        Assert.NotEmpty(result[secretName]);
    }

    [SkippableFact]
    public async Task GetSecretValuesAsync_StaticKvSecret_ReturnsDictWithValue()
    {
        SkipIfMissingCredentials();
        var secretName = RequireSecretPath("AKEYLESS_SECRET_STATIC_KV", "pam/test/pamStaticKV");

        var client = Client;
        var token = client.Authenticate(AccessId, AccessKey);
        var result = await client.GetSecretValuesAsync([secretName], token);

        Assert.True(result.ContainsKey(secretName), $"Response did not contain key '{secretName}'");
        Assert.NotEmpty(result[secretName]);
    }

    [SkippableFact]
    public async Task GetSecretValuesAsync_StaticJsonSecret_ReturnsDictWithValue()
    {
        SkipIfMissingCredentials();
        var secretName = RequireSecretPath("AKEYLESS_SECRET_STATIC_JSON", "pam/test/pamStaticJSON");

        var client = Client;
        var token = client.Authenticate(AccessId, AccessKey);
        var result = await client.GetSecretValuesAsync([secretName], token);

        Assert.True(result.ContainsKey(secretName), $"Response did not contain key '{secretName}'");
        Assert.NotEmpty(result[secretName]);
    }

    [SkippableFact]
    public async Task GetSecretValuesAsync_MultipleSecrets_ReturnsAllRequested()
    {
        SkipIfMissingCredentials();
        var secret1 = RequireSecretPath("AKEYLESS_SECRET_STATIC_TEXT", "pam/test/pamStaticTextUsername");
        var secret2 = RequireSecretPath("AKEYLESS_SECRET_STATIC_TEXT_2", "pam/test/pamStaticTextPassword");

        var client = Client;
        var token = client.Authenticate(AccessId, AccessKey);
        var result = await client.GetSecretValuesAsync([secret1, secret2], token);

        Assert.True(result.ContainsKey(secret1), $"Response did not contain key '{secret1}'");
        Assert.True(result.ContainsKey(secret2), $"Response did not contain key '{secret2}'");
    }

    [SkippableFact]
    public async Task GetSecretValuesAsync_InvalidToken_ThrowsApiException()
    {
        SkipIfMissingCredentials();

        var client = Client;
        await Assert.ThrowsAsync<ApiException>(() =>
            client.GetSecretValuesAsync(["pam/test/any"], "invalid-token"));
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Debug_K8sOrchestratorSecret_PrintsRawValue()
    {
        SkipIfMissingCredentials();
        const string secretName = "/pam/test/k8s-orchestrator";

        var client = Client;
        var token = client.Authenticate(AccessId, AccessKey);
        var result = await client.GetSecretValuesAsync([secretName], token);

        Console.WriteLine($"Keys in response: [{string.Join(", ", result.Keys)}]");
        if (result.TryGetValue(secretName, out var value))
            Console.WriteLine($"Raw value:\n{value}");
        else
            Console.WriteLine("Secret key not found in response.");

        Assert.True(result.ContainsKey(secretName), $"Response did not contain key '{secretName}'");
    }
}
