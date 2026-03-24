// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.ComponentModel.DataAnnotations;
using Keyfactor.Extensions.Pam.Akeyless;
using Keyfactor.Extensions.Pam.Akeyless.Models;
using Xunit;

namespace Keyfactor.Tests.Unit;

public class AkeylessConfigurationTests
{
    private static IList<ValidationResult> Validate(AkeylessConfiguration config)
    {
        var ctx = new ValidationContext(config);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(config, ctx, results, validateAllProperties: true);
        // Also invoke IValidatableObject.Validate explicitly since TryValidateObject may not call it
        results.AddRange(config.Validate(ctx));
        return results;
    }

    [Fact]
    public void Validate_ValidAccessKeyConfig_NoErrors()
    {
        var config = new AkeylessConfiguration
        {
            AccessId = "p-abc123",
            AccessKey = "super-secret",
            SecretType = "static_text",
            SecretName = "pam/test/secret",
            AuthType = "access_key"
        };

        var errors = Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingAccessId_ReturnsError()
    {
        var config = new AkeylessConfiguration
        {
            AccessId = "",
            AccessKey = "super-secret",
            SecretType = "static_text",
            SecretName = "pam/test/secret",
            AuthType = "access_key"
        };

        var errors = Validate(config);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AkeylessConfiguration.AccessId)));
    }

    [Fact]
    public void Validate_MissingAccessKey_ReturnsError()
    {
        var config = new AkeylessConfiguration
        {
            AccessId = "p-abc123",
            AccessKey = "",
            SecretType = "static_text",
            SecretName = "pam/test/secret",
            AuthType = "access_key"
        };

        var errors = Validate(config);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AkeylessConfiguration.AccessKey)));
    }

    [Fact]
    public void Validate_UnsupportedAuthType_ReturnsError()
    {
        var config = new AkeylessConfiguration
        {
            AccessId = "p-abc123",
            AccessKey = "super-secret",
            SecretType = "static_text",
            SecretName = "pam/test/secret",
            AuthType = "saml" // unsupported
        };

        var errors = Validate(config);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_UnsupportedSecretType_ReturnsError()
    {
        var config = new AkeylessConfiguration
        {
            AccessId = "p-abc123",
            AccessKey = "super-secret",
            SecretType = "dynamic_secret",
            SecretName = "pam/test/secret",
            AuthType = "access_key"
        };

        var errors = Validate(config);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AkeylessConfiguration.SecretType)));
    }

    [Fact]
    public void Validate_StaticKvMissingFieldName_ReturnsError()
    {
        var config = new AkeylessConfiguration
        {
            AccessId = "p-abc123",
            AccessKey = "super-secret",
            SecretType = "static_kv",
            SecretName = "pam/test/secret",
            StaticSecretFieldName = "",
            AuthType = "access_key"
        };

        var errors = Validate(config);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AkeylessConfiguration.StaticSecretFieldName)));
    }

    [Fact]
    public void Validate_StaticJsonMissingFieldName_NoError()
    {
        // StaticSecretFieldName is optional for static_json (returns full blob when omitted)
        var config = new AkeylessConfiguration
        {
            AccessId = "p-abc123",
            AccessKey = "super-secret",
            SecretType = "static_json",
            SecretName = "pam/test/secret",
            StaticSecretFieldName = "",
            AuthType = "access_key"
        };

        var errors = Validate(config);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(AkeylessConfiguration.StaticSecretFieldName)));
    }

    [Fact]
    public void SupportedSecretTypes_ContainsExpectedValues()
    {
        Assert.Contains("static_text", AkeylessConfiguration.SupportedSecretTypes);
        Assert.Contains("static_kv", AkeylessConfiguration.SupportedSecretTypes);
        Assert.Contains("static_json", AkeylessConfiguration.SupportedSecretTypes);
    }

    [Fact]
    public void Constants_DefaultAuthMethod_IsAccessKey()
    {
        Assert.Equal("access_key", AkeylessConstants.DefaultAuthMethod);
    }

    [Fact]
    public void Constants_DefaultApiUrl_IsCorrect()
    {
        Assert.Equal("https://api.akeyless.io", AkeylessConstants.DefaultAkeylessApiUrl);
    }
}
