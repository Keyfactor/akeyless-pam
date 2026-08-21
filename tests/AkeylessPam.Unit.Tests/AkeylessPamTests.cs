// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using akeyless.Client;
using Keyfactor.Extensions.Pam.Akeyless;
using Moq;
using Xunit;

namespace Keyfactor.Tests.Unit;

/// <summary>
/// Helper to build the standard valid server config dictionary.
/// </summary>
internal static class Params
{
    internal static Dictionary<string, string> ValidServer(
        string authType = "access_key",
        string accessId = "test-id",
        string accessKey = "test-key",
        string url = "https://api.akeyless.io") => new()
    {
        ["AuthType"] = authType,
        ["AccessId"] = accessId,
        ["AccessKey"] = accessKey,
        ["Url"] = url
    };

    internal static Dictionary<string, string> Instance(
        string secretName = "pam/test/secret",
        string secretType = "static_text",
        string? fieldName = null)
    {
        var d = new Dictionary<string, string>
        {
            ["SecretName"] = secretName,
            ["SecretType"] = secretType
        };
        if (fieldName != null) d["StaticSecretFieldName"] = fieldName;
        return d;
    }
}

public class ValidationTests
{
    [Fact]
    public void GetPassword_MissingSecretName_ThrowsInvalidClientConfigurationException()
    {
        var pam = new AkeylessPam(_ => Mock.Of<IAkeylessApiClient>());
        var instance = new Dictionary<string, string> { ["SecretType"] = "static_text" }; // no SecretName

        Assert.Throws<InvalidClientConfigurationException>(() =>
            pam.GetPassword(instance, Params.ValidServer()));
    }

    [Fact]
    public void GetPassword_MissingAccessId_ThrowsInvalidClientConfigurationException()
    {
        var pam = new AkeylessPam(_ => Mock.Of<IAkeylessApiClient>());
        var server = new Dictionary<string, string>
        {
            ["AuthType"] = "access_key",
            ["AccessKey"] = "test-key"
            // no AccessId
        };

        Assert.Throws<InvalidClientConfigurationException>(() =>
            pam.GetPassword(Params.Instance(), server));
    }

    [Fact]
    public void GetPassword_MissingAccessKey_ThrowsInvalidClientConfigurationException()
    {
        var pam = new AkeylessPam(_ => Mock.Of<IAkeylessApiClient>());
        var server = new Dictionary<string, string>
        {
            ["AuthType"] = "access_key",
            ["AccessId"] = "test-id"
            // no AccessKey
        };

        Assert.Throws<InvalidClientConfigurationException>(() =>
            pam.GetPassword(Params.Instance(), server));
    }

    [Fact]
    public void GetPassword_InvalidAuthType_Throws()
    {
        var pam = new AkeylessPam(_ => Mock.Of<IAkeylessApiClient>());
        var server = new Dictionary<string, string>
        {
            ["AuthType"] = "unsupported_auth",
            ["AccessId"] = "test-id",
            ["AccessKey"] = "test-key"
        };

        Assert.Throws<Exception>(() =>
            pam.GetPassword(Params.Instance(), server));
    }

    [Fact]
    public void GetPassword_InvalidSecretType_ThrowsInvalidClientConfigurationException()
    {
        var pam = new AkeylessPam(_ => Mock.Of<IAkeylessApiClient>());
        var instance = Params.Instance(secretType: "dynamic_secret");

        Assert.Throws<InvalidClientConfigurationException>(() =>
            pam.GetPassword(instance, Params.ValidServer()));
    }
}

public class AuthenticationTests
{
    [Fact]
    public void GetPassword_AuthenticateReturnsEmptyToken_ThrowsInvalidTokenException()
    {
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(string.Empty);

        var pam = new AkeylessPam(_ => mock.Object);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer()));

        Assert.IsType<InvalidTokenException>(ex.InnerException);
    }

    [Fact]
    public void GetPassword_AuthenticateThrowsApiException_WrapsAsInvalidClientConfigurationException()
    {
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new ApiException(401, "Unauthorized"));

        var pam = new AkeylessPam(_ => mock.Object);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer()));

        Assert.IsType<InvalidClientConfigurationException>(ex.InnerException);
    }

    [Fact]
    public void GetPassword_UsesConfiguredUrl_WhenNoEnvVar()
    {
        string? capturedBasePath = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(basePath =>
        {
            capturedBasePath = basePath;
            return mock.Object;
        });

        pam.GetPassword(Params.Instance(), Params.ValidServer(url: "https://custom.akeyless.io"));

        Assert.Equal("https://custom.akeyless.io", capturedBasePath);
    }
}

public class ConfiguredValueSanitizationTests
{
    // Mirrors the env-var-override trimming tests: Command-portal/manifest.json-configured values
    // commonly carry an incidental trailing newline too (e.g. from hand-editing JSON or a paste with a
    // stray line terminator), and BuildAkeylessConfiguration must trim it the same way
    // ResolveEnvOverride trims env-var-sourced values, rather than hard-failing on a previously-benign
    // artifact.

    [Fact]
    public void GetPassword_ConfiguredUrlHasTrailingNewline_IsTrimmedBeforeUse()
    {
        // Cleared, not just left alone, so this test is hermetic against a real AKEYLESS_API_URL
        // already present in the ambient environment (e.g. a developer's shell).
        using var _ = new EnvVarScope("AKEYLESS_API_URL", null);

        string? capturedBasePath = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(basePath =>
        {
            capturedBasePath = basePath;
            return mock.Object;
        });

        pam.GetPassword(Params.Instance(), Params.ValidServer(url: "https://api.akeyless.io\n"));

        Assert.Equal("https://api.akeyless.io", capturedBasePath);
    }

    [Fact]
    public void GetPassword_ConfiguredAuthTypeHasTrailingNewline_IsTrimmedBeforeUse()
    {
        using var _ = new EnvVarScope("AKEYLESS_AUTH_TYPE", null);

        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(authType: "access_key\n"));

        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void GetPassword_ConfiguredAccessIdHasTrailingNewline_IsTrimmedBeforeUse()
    {
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_ID", null);

        string? capturedAccessId = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((accessId, _) => capturedAccessId = accessId)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessId: "configured-access-id\n"));

        Assert.Equal("configured-access-id", capturedAccessId);
    }

    [Fact]
    public void GetPassword_ConfiguredAccessKeyHasTrailingNewline_IsTrimmedBeforeUse()
    {
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_KEY", null);

        string? capturedAccessKey = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, accessKey) => capturedAccessKey = accessKey)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessKey: "configured-access-key\n"));

        Assert.Equal("configured-access-key", capturedAccessKey);
    }

    [Fact]
    public void GetPassword_ConfiguredAuthTypeKeyPresentButNull_DefaultsToAccessKeyInsteadOfThrowingNullReferenceException()
    {
        using var _ = new EnvVarScope("AKEYLESS_AUTH_TYPE", null);

        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);
        var server = Params.ValidServer();
        server["AuthType"] = null!; // key present, value null — legal for Dictionary<string, string>

        pam.GetPassword(Params.Instance(), server);

        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void GetPassword_ConfiguredUrlKeyPresentButNull_FallsBackToDefaultInsteadOfThrowingNullReferenceException()
    {
        using var _ = new EnvVarScope("AKEYLESS_API_URL", null);

        string? capturedBasePath = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(basePath =>
        {
            capturedBasePath = basePath;
            return mock.Object;
        });
        var server = Params.ValidServer();
        server["Url"] = null!; // key present, value null — legal for Dictionary<string, string>

        pam.GetPassword(Params.Instance(), server);

        Assert.Equal("https://api.akeyless.io", capturedBasePath);
    }

    [Fact]
    public void GetPassword_ConfiguredUrlIsWhitespaceOnly_FallsBackToDefaultInsteadOfEmptyBasePath()
    {
        using var _ = new EnvVarScope("AKEYLESS_API_URL", null);

        string? capturedBasePath = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(basePath =>
        {
            capturedBasePath = basePath;
            return mock.Object;
        });

        pam.GetPassword(Params.Instance(), Params.ValidServer(url: "   "));

        Assert.Equal("https://api.akeyless.io", capturedBasePath);
    }
}

public class EnvironmentVariableOverrideTests
{
    [Fact]
    public void GetPassword_AkeylessApiUrlEnvVar_OverridesConfiguredUrl()
    {
        using var _ = new EnvVarScope("AKEYLESS_API_URL", "https://env-override.akeyless.io");

        string? capturedBasePath = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>())).Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(basePath =>
        {
            capturedBasePath = basePath;
            return mock.Object;
        });

        pam.GetPassword(Params.Instance(), Params.ValidServer(url: "https://configured.akeyless.io"));

        Assert.Equal("https://env-override.akeyless.io", capturedBasePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPassword_AkeylessApiUrlEnvVarUnsetOrBlank_FallsBackToConfiguredUrl(string? envValue)
    {
        using var _ = new EnvVarScope("AKEYLESS_API_URL", envValue);

        string? capturedBasePath = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>())).Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(basePath =>
        {
            capturedBasePath = basePath;
            return mock.Object;
        });

        pam.GetPassword(Params.Instance(), Params.ValidServer(url: "https://configured.akeyless.io"));

        Assert.Equal("https://configured.akeyless.io", capturedBasePath);
    }

    [Fact]
    public void GetPassword_AkeylessAccessIdEnvVar_OverridesConfiguredAccessId()
    {
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_ID", "env-access-id");

        string? capturedAccessId = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((accessId, _) => capturedAccessId = accessId)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessId: "configured-access-id"));

        Assert.Equal("env-access-id", capturedAccessId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPassword_AkeylessAccessIdEnvVarUnsetOrBlank_FallsBackToConfiguredAccessId(string? envValue)
    {
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_ID", envValue);

        string? capturedAccessId = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((accessId, _) => capturedAccessId = accessId)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessId: "configured-access-id"));

        Assert.Equal("configured-access-id", capturedAccessId);
    }

    [Fact]
    public void GetPassword_AkeylessAccessIdEnvVarHasTrailingNewline_IsTrimmedBeforeUse()
    {
        // A trailing newline is a common artifact of file-mounted/envFrom secret provisioning; it must not
        // be sent to Akeyless as part of the credential.
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_ID", "env-access-id\n");

        string? capturedAccessId = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((accessId, _) => capturedAccessId = accessId)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessId: "configured-access-id"));

        Assert.Equal("env-access-id", capturedAccessId);
    }

    [Theory]
    [InlineData("env-access-id\nforged log line")] // embedded LF, survives Trim()
    [InlineData("env-access-id\u2028forged log line")] // Unicode LINE SEPARATOR
    [InlineData("env-access-id\u2029forged log line")] // Unicode PARAGRAPH SEPARATOR
    [InlineData("env-access-id[2Ktampered")] // ANSI escape sequence (ESC is a control character)
    [InlineData("env-access-id\u202Ehidden-suffix")] // Unicode RIGHT-TO-LEFT OVERRIDE (Format category)
    [InlineData("env-access-id\uFE0Ftampered")] // Unicode Variation Selector (Nonspacing-Mark category)
    public void GetPassword_AkeylessAccessIdEnvVarHasEmbeddedLineBreakOrControlCharacter_ThrowsInvalidClientConfigurationException(
        string maliciousValue)
    {
        // Non-ASCII line-terminating separators, ANSI escape sequences, bidi-override characters, and
        // zero-width variation selectors can all equally forge or tamper with rendered log output. Rather
        // than blocklisting each character class as it's discovered, the printable-ASCII allowlist rejects
        // all of them (and anything else non-ASCII) in one check.
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_ID", maliciousValue);

        var mock = new Mock<IAkeylessApiClient>();
        var pam = new AkeylessPam(_ => mock.Object);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer(accessId: "configured-access-id")));

        Assert.IsType<InvalidClientConfigurationException>(ex.InnerException);
        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetPassword_ConfiguredAccessIdHasEmbeddedControlCharacter_ThrowsInvalidClientConfigurationException()
    {
        // The printable-ASCII allowlist is applied to the configured value in BuildAkeylessConfiguration
        // (before its own log statements echo it), not just to the final resolved value in InitClient, so
        // a malicious/malformed AccessId supplied via Command's server configuration (no env var override
        // involved) is rejected too -- not just the override path. Explicitly unset the env var so an
        // ambient AKEYLESS_ACCESS_ID on the host running the tests can't silently override it.
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_ID", null);

        var mock = new Mock<IAkeylessApiClient>();
        var pam = new AkeylessPam(_ => mock.Object);

        // BuildAkeylessConfiguration runs synchronously (before the .Result call boundary), so this throws
        // directly rather than wrapped in an AggregateException.
        Assert.Throws<InvalidClientConfigurationException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer(accessId: "configured-id\r\nforged log line")));

        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetPassword_ConfiguredUrlHasEmbeddedControlCharacter_ThrowsInvalidClientConfigurationException()
    {
        // Same as above but for Url: BuildAkeylessConfiguration's own "Using Akeyless URL ..." debug log
        // echoes config.Url before InitClient is ever reached, so this must be validated at that point too.
        using var _ = new EnvVarScope("AKEYLESS_API_URL", null);

        var mock = new Mock<IAkeylessApiClient>();
        var pam = new AkeylessPam(_ => mock.Object);

        Assert.Throws<InvalidClientConfigurationException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer(url: "https://api.akeyless.io\r\nforged log line")));

        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetPassword_AkeylessAccessKeyEnvVar_OverridesConfiguredAccessKey()
    {
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_KEY", "env-access-key");

        string? capturedAccessKey = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, accessKey) => capturedAccessKey = accessKey)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessKey: "configured-access-key"));

        Assert.Equal("env-access-key", capturedAccessKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPassword_AkeylessAccessKeyEnvVarUnsetOrBlank_FallsBackToConfiguredAccessKey(string? envValue)
    {
        using var _ = new EnvVarScope("AKEYLESS_ACCESS_KEY", envValue);

        string? capturedAccessKey = null;
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, accessKey) => capturedAccessKey = accessKey)
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(accessKey: "configured-access-key"));

        Assert.Equal("configured-access-key", capturedAccessKey);
    }

    [Fact]
    public void GetPassword_AkeylessAuthTypeEnvVarUnrecognised_ThrowsInvalidClientConfigurationException()
    {
        // Override the configured "access_key" auth type with an unrecognised value via env var — this must
        // fail fast rather than silently skipping authentication and proceeding with an unauthenticated request.
        using var _ = new EnvVarScope("AKEYLESS_AUTH_TYPE", "unrecognised_env_auth_type");

        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer(authType: "access_key")));

        Assert.IsType<InvalidClientConfigurationException>(ex.InnerException);
        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // A trailing newline (e.g. from file-mounted/envFrom secret provisioning) must not cause the
    // resolved auth type to fail the "access_key" match and fall into the unsupported-auth-type path.
    [InlineData("access_key\n")]
    public void GetPassword_AkeylessAuthTypeEnvVarUnsetOrBlankOrHasTrailingNewline_FallsBackToOrTrims(
        string? envValue)
    {
        using var _ = new EnvVarScope("AKEYLESS_AUTH_TYPE", envValue);

        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>())).Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { ["pam/test/secret"] = "value" });

        var pam = new AkeylessPam(_ => mock.Object);

        pam.GetPassword(Params.Instance(), Params.ValidServer(authType: "access_key"));

        mock.Verify(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}

public class SecretRetrievalTests
{
    private static AkeylessPam PamWithMockReturning(string secretName, string secretValue)
    {
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string> { [secretName] = secretValue });
        return new AkeylessPam(_ => mock.Object);
    }

    [Fact]
    public void GetPassword_StaticText_PlainString_ReturnsAsIs()
    {
        var pam = PamWithMockReturning("pam/test/secret", "my-password");

        var result = pam.GetPassword(Params.Instance(), Params.ValidServer());

        Assert.Equal("my-password", result);
    }

    [Fact]
    public void GetPassword_GetSecretValuesThrowsApiException_WrapsAsInvalidSecretConfigurationException()
    {
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .Throws(new ApiException(404, "Not Found: request body echo including fake-token"));

        var pam = new AkeylessPam(_ => mock.Object);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer()));

        var inner = Assert.IsType<InvalidSecretConfigurationException>(ex.InnerException);
        Assert.DoesNotContain("fake-token", inner.Message);
    }

    [Fact]
    public void GetPassword_StaticText_JsonContent_ReturnsFullJsonBlob()
    {
        const string json = "{\"username\":\"admin\",\"password\":\"s3cr3t\"}";
        var pam = PamWithMockReturning("pam/test/secret", json);

        var result = pam.GetPassword(Params.Instance(secretType: "static_text"), Params.ValidServer());

        Assert.Equal(json, result);
    }

    [Fact]
    public void GetPassword_StaticKv_ReturnsMatchingFieldValue()
    {
        const string kvContent = "username=admin\npassword=s3cr3t\n";
        var pam = PamWithMockReturning("pam/test/secret", kvContent);

        var result = pam.GetPassword(
            Params.Instance(secretType: "static_kv", fieldName: "password"),
            Params.ValidServer());

        Assert.Equal("s3cr3t", result);
    }

    [Fact]
    public void GetPassword_StaticKv_MissingField_ThrowsInvalidSecretConfigurationException()
    {
        const string kvContent = "username=admin\npassword=s3cr3t\n";
        var pam = PamWithMockReturning("pam/test/secret", kvContent);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(
                Params.Instance(secretType: "static_kv", fieldName: "nonexistent"),
                Params.ValidServer()));

        Assert.IsType<InvalidSecretConfigurationException>(ex.InnerException);
    }

    [Fact]
    public void GetPassword_StaticJson_ReturnsSpecifiedField()
    {
        const string json = "{\"username\":\"admin\",\"password\":\"s3cr3t\"}";
        var pam = PamWithMockReturning("pam/test/secret", json);

        var result = pam.GetPassword(
            Params.Instance(secretType: "static_json", fieldName: "username"),
            Params.ValidServer());

        Assert.Equal("admin", result);
    }

    [Fact]
    public void GetPassword_StaticJson_NoFieldName_ReturnsFullBlob()
    {
        const string json = "{\"username\":\"admin\",\"password\":\"s3cr3t\"}";
        var pam = PamWithMockReturning("pam/test/secret", json);

        var result = pam.GetPassword(
            Params.Instance(secretType: "static_json"), // no fieldName
            Params.ValidServer());

        Assert.Equal(json, result);
    }

    [Fact]
    public void GetPassword_StaticJson_MissingField_ThrowsInvalidSecretConfigurationException()
    {
        const string json = "{\"username\":\"admin\"}";
        var pam = PamWithMockReturning("pam/test/secret", json);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(
                Params.Instance(secretType: "static_json", fieldName: "nonexistent"),
                Params.ValidServer()));

        Assert.IsType<InvalidSecretConfigurationException>(ex.InnerException);
    }

    [Fact]
    public void GetPassword_SecretNotInResponse_ThrowsInvalidSecretConfigurationException()
    {
        var mock = new Mock<IAkeylessApiClient>();
        mock.Setup(c => c.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        mock.Setup(c => c.GetSecretValuesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>()); // empty — secret not found

        var pam = new AkeylessPam(_ => mock.Object);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer()));

        Assert.IsType<InvalidSecretConfigurationException>(ex.InnerException);
    }

    [Fact]
    public void GetPassword_EmptySecretValue_ThrowsInvalidSecretConfigurationException()
    {
        var pam = PamWithMockReturning("pam/test/secret", string.Empty);

        var ex = Assert.Throws<AggregateException>(() =>
            pam.GetPassword(Params.Instance(), Params.ValidServer()));

        Assert.IsType<InvalidSecretConfigurationException>(ex.InnerException);
    }

    [Fact]
    public void GetPassword_StaticJson_WhitespaceFieldName_ReturnsFullBlob()
    {
        // Command UI may send whitespace instead of empty string — should be treated as no field name
        const string json = "{\"username\":\"admin\",\"password\":\"s3cr3t\"}";
        var pam = PamWithMockReturning("pam/test/secret", json);

        var result = pam.GetPassword(
            Params.Instance(secretType: "static_json", fieldName: "   "),
            Params.ValidServer());

        Assert.Equal(json, result);
    }

    [Fact]
    public void GetPassword_StaticKv_JsonStoredAsKv_ParsesViaJson()
    {
        // When SecretType is static_kv but content is JSON, ParseJsonSecret is used
        const string json = "{\"username\":\"admin\",\"password\":\"s3cr3t\"}";
        var pam = PamWithMockReturning("pam/test/secret", json);

        var result = pam.GetPassword(
            Params.Instance(secretType: "static_kv", fieldName: "password"),
            Params.ValidServer());

        Assert.Equal("s3cr3t", result);
    }
}
