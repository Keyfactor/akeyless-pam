## Testing

The test suite is split into two projects under `tests/`:

| Project | Purpose |
|---|---|
| `AkeylessPam.Unit.Tests` | Pure unit tests — no network, always runnable |
| `AkeylessPam.Integration.Tests` | Tests against a live Akeyless instance — skip automatically when credentials are absent |

### Running Tests

A `Makefile` at the repo root provides shortcuts for common tasks:

```shell
make test-unit         # unit tests only
make test-integration  # integration tests only
make test              # both test projects
```

Or use `dotnet` directly:

```shell
# Unit tests only
dotnet test tests/AkeylessPam.Unit.Tests/

# Integration tests only
dotnet test tests/AkeylessPam.Integration.Tests/
```

Integration tests load credentials from environment variables. As a convenience for local development, they also read a `.env` file in the repository root if present. Environment variables always take precedence over `.env` values.

#### Required environment variables

| Variable | Description |
|---|---|
| `AKEYLESS_ACCESS_ID` | Akeyless access key ID |
| `AKEYLESS_ACCESS_KEY` | Akeyless access key secret |

#### Optional environment variables

| Variable | Default | Description |
|---|---|---|
| `AKEYLESS_API_URL` | `https://api.akeyless.io` | Akeyless API base URL |
| `AKEYLESS_AUTH_TYPE` | `access_key` | Auth type |
| `AKEYLESS_SECRET_STATIC_TEXT` | `pam/test/pamStaticTextUsername` | Path to a `static_text` secret |
| `AKEYLESS_SECRET_STATIC_TEXT_2` | `pam/test/pamStaticTextPassword` | Path to a second `static_text` secret (used in multi-secret test) |
| `AKEYLESS_SECRET_STATIC_KV` | `pam/test/pamStaticKV` | Path to a `static_kv` secret with `username` and `password` fields |
| `AKEYLESS_SECRET_STATIC_JSON` | `pam/test/pamStaticJSON` | Path to a `static_json` secret with `username` and `password` fields |
| `AKEYLESS_SECRET_STATIC_JSON_RAW` | — | Path to a `static_json` secret to retrieve as a raw blob (no field extraction) |

### Unit Test Cases

#### Validation (`ValidationTests`)

| Test | What it verifies |
|---|---|
| `GetPassword_MissingSecretName_ThrowsInvalidClientConfigurationException` | `SecretName` absent in instance parameters → exception |
| `GetPassword_MissingAccessId_ThrowsInvalidClientConfigurationException` | `AccessId` absent in server parameters → exception |
| `GetPassword_MissingAccessKey_ThrowsInvalidClientConfigurationException` | `AccessKey` absent in server parameters → exception |
| `GetPassword_InvalidAuthType_Throws` | Unknown `AuthType` value → exception |
| `GetPassword_InvalidSecretType_ThrowsInvalidClientConfigurationException` | Unknown `SecretType` value → `InvalidClientConfigurationException` (caught at model validation before the async path) |

#### Authentication (`AuthenticationTests`)

| Test | What it verifies |
|---|---|
| `GetPassword_AuthenticateReturnsEmptyToken_ThrowsInvalidTokenException` | Mock returns empty token → `InvalidTokenException` |
| `GetPassword_UsesConfiguredUrl_WhenNoEnvVar` | The `Url` server parameter is forwarded to the client factory as `basePath` |

#### Secret Retrieval (`SecretRetrievalTests`)

| Test | What it verifies |
|---|---|
| `GetPassword_StaticText_PlainString_ReturnsAsIs` | Plain text secret returned unchanged |
| `GetPassword_StaticText_JsonContent_ReturnsFullJsonBlob` | JSON-shaped content with `SecretType=static_text` returned as raw string |
| `GetPassword_StaticKv_ReturnsMatchingFieldValue` | KV-formatted content, field found → correct value |
| `GetPassword_StaticKv_MissingField_ThrowsInvalidSecretConfigurationException` | KV content, requested field absent → exception |
| `GetPassword_StaticKv_JsonStoredAsKv_ParsesViaJson` | JSON-shaped content with `SecretType=static_kv` is parsed as JSON (auto-detection) |
| `GetPassword_StaticJson_ReturnsSpecifiedField` | JSON content, field name provided → field value |
| `GetPassword_StaticJson_NoFieldName_ReturnsFullBlob` | JSON content, no `StaticSecretFieldName` → full JSON blob |
| `GetPassword_StaticJson_MissingField_ThrowsInvalidSecretConfigurationException` | JSON content, requested field absent → exception |
| `GetPassword_SecretNotInResponse_ThrowsInvalidSecretConfigurationException` | API response does not contain the requested secret name → exception |
| `GetPassword_EmptySecretValue_ThrowsInvalidSecretConfigurationException` | Secret found but value is empty → exception |

#### Configuration Model (`AkeylessConfigurationTests`)

| Test | What it verifies |
|---|---|
| `Validate_ValidAccessKeyConfig_NoErrors` | A fully populated valid config produces no validation errors |
| `Validate_MissingAccessId_ReturnsError` | Empty `AccessId` with `access_key` auth → validation error |
| `Validate_MissingAccessKey_ReturnsError` | Empty `AccessKey` with `access_key` auth → validation error |
| `Validate_UnsupportedAuthType_ReturnsError` | Auth type not in the supported list → validation error |
| `Validate_UnsupportedSecretType_ReturnsError` | `SecretType` not in `[static_text, static_kv, static_json]` → validation error |
| `Validate_StaticKvMissingFieldName_ReturnsError` | `static_kv` with empty `StaticSecretFieldName` → validation error |
| `Validate_StaticJsonMissingFieldName_NoError` | `static_json` with empty `StaticSecretFieldName` is valid (field is optional — returns full blob) |
| `SupportedSecretTypes_ContainsExpectedValues` | Static list contains all three supported types |
| `Constants_DefaultAuthMethod_IsAccessKey` | Default auth method constants are `access_key` |
| `Constants_DefaultApiUrl_IsCorrect` | Default API URL is `https://api.akeyless.io` |

### Integration Test Cases

#### `AkeylessApiClientTests` — exercises `AkeylessApiClient` directly

| Test | Requires |
|---|---|
| `Authenticate_ValidCredentials_ReturnsNonEmptyToken` | Credentials |
| `Authenticate_InvalidCredentials_ThrowsApiException` | Credentials |
| `GetSecretValuesAsync_StaticTextSecret_ReturnsDictWithValue` | Credentials, `AKEYLESS_SECRET_STATIC_TEXT` |
| `GetSecretValuesAsync_StaticKvSecret_ReturnsDictWithValue` | Credentials, `AKEYLESS_SECRET_STATIC_KV` |
| `GetSecretValuesAsync_StaticJsonSecret_ReturnsDictWithValue` | Credentials, `AKEYLESS_SECRET_STATIC_JSON` |
| `GetSecretValuesAsync_MultipleSecrets_ReturnsAllRequested` | Credentials, `AKEYLESS_SECRET_STATIC_TEXT` + `AKEYLESS_SECRET_STATIC_TEXT_2` |
| `GetSecretValuesAsync_InvalidToken_ThrowsApiException` | Credentials |

#### `AkeylessPamIntegrationTests` — exercises the full `AkeylessPam.GetPassword()` stack

| Test | Requires |
|---|---|
| `GetPassword_StaticText_ReturnsNonEmptyValue` | Credentials, `AKEYLESS_SECRET_STATIC_TEXT` |
| `GetPassword_StaticKv_UsernameField_ReturnsValue` | Credentials, `AKEYLESS_SECRET_STATIC_KV` |
| `GetPassword_StaticKv_PasswordField_ReturnsValue` | Credentials, `AKEYLESS_SECRET_STATIC_KV` |
| `GetPassword_StaticJson_UsernameField_ReturnsValue` | Credentials, `AKEYLESS_SECRET_STATIC_JSON` |
| `GetPassword_StaticJson_PasswordField_ReturnsValue` | Credentials, `AKEYLESS_SECRET_STATIC_JSON` |
| `GetPassword_StaticJson_NoFieldName_ReturnsRawJsonBlob` | Credentials, `AKEYLESS_SECRET_STATIC_JSON_RAW` |
| `GetPassword_BadCredentials_ThrowsInvalidClientConfigurationException` | `AKEYLESS_SECRET_STATIC_TEXT` (uses deliberately wrong credentials) |
| `GetPassword_NonexistentSecret_ThrowsException` | Credentials (uses hardcoded nonexistent path) |
