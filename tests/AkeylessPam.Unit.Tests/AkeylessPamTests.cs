// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

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
