# AkeylessPam.Unit.Tests

Unit tests for the Akeyless PAM Provider. Tests run entirely in-process with no external dependencies — the Akeyless API is replaced by a Moq mock of `IAkeylessApiClient`.

## Running

```shell
# Via Makefile
make test-unit

# Or directly
dotnet test tests/AkeylessPam.Unit.Tests/
```

No environment variables or credentials required.

## Test Files

### `AkeylessPamTests.cs`

Tests for `AkeylessPam.GetPassword()` covering configuration validation, authentication behavior, and secret parsing logic. The Akeyless API client is mocked so all tests are deterministic and offline.

#### ValidationTests

| Test | Description |
|------|-------------|
| `GetPassword_MissingSecretName_ThrowsInvalidClientConfigurationException` | Throws when `SecretName` is absent from instance parameters |
| `GetPassword_MissingAccessId_ThrowsInvalidClientConfigurationException` | Throws when `AccessId` is absent from server parameters |
| `GetPassword_MissingAccessKey_ThrowsInvalidClientConfigurationException` | Throws when `AccessKey` is absent from server parameters |
| `GetPassword_InvalidAuthType_Throws` | Throws when `AuthType` is not a recognized value |
| `GetPassword_InvalidSecretType_ThrowsInvalidClientConfigurationException` | Throws when `SecretType` is not one of `static_text`, `static_json`, `static_kv` |

#### AuthenticationTests

| Test | Description |
|------|-------------|
| `GetPassword_AuthenticateReturnsEmptyToken_ThrowsInvalidTokenException` | When the API client returns an empty token, throws `InvalidTokenException` wrapped in `AggregateException` |
| `GetPassword_UsesConfiguredUrl_WhenNoEnvVar` | The URL passed to the API client factory matches the `Url` server parameter |

#### SecretRetrievalTests

| Test | Description |
|------|-------------|
| `GetPassword_StaticText_PlainString_ReturnsAsIs` | A plain-text `static_text` secret is returned unchanged |
| `GetPassword_StaticText_JsonContent_ReturnsFullJsonBlob` | A JSON-formatted value stored as `static_text` is returned as the full JSON string |
| `GetPassword_StaticKv_ReturnsMatchingFieldValue` | A `static_kv` secret returns the value of the field named by `StaticSecretFieldName` |
| `GetPassword_StaticKv_MissingField_ThrowsInvalidSecretConfigurationException` | Throws `InvalidSecretConfigurationException` when `StaticSecretFieldName` does not exist in the KV secret |
| `GetPassword_StaticJson_ReturnsSpecifiedField` | A `static_json` secret returns the value of the field named by `StaticSecretFieldName` |
| `GetPassword_StaticJson_NoFieldName_ReturnsFullBlob` | A `static_json` secret without `StaticSecretFieldName` returns the full JSON blob |
| `GetPassword_StaticJson_MissingField_ThrowsInvalidSecretConfigurationException` | Throws `InvalidSecretConfigurationException` when `StaticSecretFieldName` is not present in the JSON |
| `GetPassword_SecretNotInResponse_ThrowsInvalidSecretConfigurationException` | Throws `InvalidSecretConfigurationException` when the API returns an empty dictionary (secret not found) |
| `GetPassword_EmptySecretValue_ThrowsInvalidSecretConfigurationException` | Throws `InvalidSecretConfigurationException` when the API returns an empty string for the secret value |
| `GetPassword_StaticJson_WhitespaceFieldName_ReturnsFullBlob` | A `static_json` secret with a whitespace-only `StaticSecretFieldName` (e.g. a space from the Command UI) returns the full JSON blob |
| `GetPassword_StaticKv_JsonStoredAsKv_ParsesViaJson` | When a `static_kv` secret contains JSON instead of `key=value` lines, falls back to JSON parsing |

---

### `AkeylessConfigurationTests.cs`

Tests for `AkeylessConfiguration` — validating configuration model constraints, supported type lists, and constant values.

| Test | Description |
|------|-------------|
| `Validate_ValidAccessKeyConfig_NoErrors` | A fully populated `access_key` configuration produces no validation errors |
| `Validate_MissingAccessId_ReturnsError` | Empty `AccessId` produces a validation error referencing the `AccessId` field |
| `Validate_MissingAccessKey_ReturnsError` | Empty `AccessKey` produces a validation error referencing the `AccessKey` field |
| `Validate_UnsupportedAuthType_ReturnsError` | An unsupported `AuthType` (e.g. `saml`) produces a validation error |
| `Validate_UnsupportedSecretType_ReturnsError` | An unsupported `SecretType` (e.g. `dynamic_secret`) produces a validation error referencing the `SecretType` field |
| `Validate_StaticKvMissingFieldName_ReturnsError` | Empty `StaticSecretFieldName` with `SecretType = static_kv` produces a validation error |
| `Validate_StaticJsonMissingFieldName_NoError` | Empty `StaticSecretFieldName` with `SecretType = static_json` produces no validation error (field is optional for JSON) |
| `SupportedSecretTypes_ContainsExpectedValues` | `AkeylessConfiguration.SupportedSecretTypes` contains `static_text`, `static_kv`, and `static_json` |
| `Constants_DefaultAuthMethod_IsAccessKey` | `AkeylessConstants.DefaultAuthMethod` and `DefaultAuthMethodReadOnly` are both `access_key` |
| `Constants_DefaultApiUrl_IsCorrect` | `AkeylessConstants.DefaultAkeylessApiUrl` is `https://api.akeyless.io` |
