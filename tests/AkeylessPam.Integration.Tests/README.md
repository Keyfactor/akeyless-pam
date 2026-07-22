# AkeylessPam.Integration.Tests

Integration tests for the Akeyless PAM Provider that connect to a live Akeyless instance. All tests are marked `[SkippableFact]` and skip automatically when the required environment variables are not set, making them safe to run in CI without credentials configured.

## Running

```shell
# Via Makefile
make test-integration

# Or directly
dotnet test tests/AkeylessPam.Integration.Tests/
```

Credentials can be provided via environment variables or a `.env` file in the repo root. The `.env` file is loaded automatically by the test setup and does not override variables already set in the environment (safe for CI use).

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `AKEYLESS_ACCESS_ID` | Yes | — | Akeyless Access ID (API key) |
| `AKEYLESS_ACCESS_KEY` | Yes | — | Akeyless Access Key secret |
| `AKEYLESS_API_URL` | No | `https://api.akeyless.io` | Akeyless API endpoint |
| `AKEYLESS_AUTH_TYPE` | No | `access_key` | Auth type passed to the provider |
| `AKEYLESS_SECRET_STATIC_TEXT` | Per-test | `pam/test/pamStaticTextUsername` | Path to a `static_text` secret |
| `AKEYLESS_SECRET_STATIC_TEXT_2` | Per-test | `pam/test/pamStaticTextPassword` | Path to a second `static_text` secret (used in multi-secret test) |
| `AKEYLESS_SECRET_STATIC_KV` | Per-test | `pam/test/pamStaticKV` | Path to a `static_kv` secret with `username` and `password` fields |
| `AKEYLESS_SECRET_STATIC_JSON` | Per-test | `pam/test/pamStaticJSON` | Path to a `static_json` secret with `username` and `password` fields |
| `AKEYLESS_SECRET_STATIC_JSON_RAW` | Per-test | — | Path to a `static_json` secret to retrieve as a raw blob (no field extraction) |

## Test Files

### `AkeylessPamIntegrationTests.cs`

End-to-end tests for `AkeylessPam.GetPassword()` against a live Akeyless instance. Each test constructs server and instance parameter dictionaries the same way Keyfactor Command would, then calls the provider.

| Test | Requires | Description |
|------|----------|-------------|
| `GetPassword_StaticText_ReturnsNonEmptyValue` | credentials + `AKEYLESS_SECRET_STATIC_TEXT` | Retrieves a `static_text` secret and asserts a non-empty value is returned |
| `GetPassword_StaticKv_UsernameField_ReturnsValue` | credentials + `AKEYLESS_SECRET_STATIC_KV` | Retrieves the `username` field from a `static_kv` secret |
| `GetPassword_StaticKv_PasswordField_ReturnsValue` | credentials + `AKEYLESS_SECRET_STATIC_KV` | Retrieves the `password` field from a `static_kv` secret |
| `GetPassword_StaticJson_UsernameField_ReturnsValue` | credentials + `AKEYLESS_SECRET_STATIC_JSON` | Retrieves the `username` field from a `static_json` secret |
| `GetPassword_StaticJson_PasswordField_ReturnsValue` | credentials + `AKEYLESS_SECRET_STATIC_JSON` | Retrieves the `password` field from a `static_json` secret |
| `GetPassword_StaticJson_NoFieldName_ReturnsRawJsonBlob` | credentials + `AKEYLESS_SECRET_STATIC_JSON_RAW` | Retrieves a `static_json` secret without specifying a field, asserts result is a JSON object or array |
| `GetPassword_BadCredentials_ThrowsInvalidClientConfigurationException` | `AKEYLESS_SECRET_STATIC_TEXT` | Intentionally uses invalid credentials and asserts `InvalidClientConfigurationException` is thrown. Clears `AKEYLESS_ACCESS_ID`/`AKEYLESS_ACCESS_KEY` for the duration of the test so the env var override feature doesn't replace the bad credentials with real ones |
| `GetPassword_NonexistentSecret_ThrowsException` | credentials | Requests a secret path that does not exist and asserts an exception is thrown |
| `GetPassword_StaticJson_NoFieldName_ReturnsRawJsonBlob_K8sOrchestratorSecret` | credentials | Retrieves `/pam/test/k8s-orchestrator` as `static_json` with no field name and asserts the full JSON blob is returned |
| `GetPassword_StaticJson_WhitespaceFieldName_ReturnsRawJsonBlob_K8sOrchestratorSecret` | credentials | Same as above but passes a whitespace-only `StaticSecretFieldName` (simulating the Keyfactor Command portal behavior); asserts the full JSON blob is returned |

---

### `AkeylessApiClientTests.cs`

Lower-level tests for `AkeylessApiClient` — the adapter that wraps the Akeyless SDK `V2Api`. These tests exercise authentication and secret retrieval directly without going through the PAM provider layer.

| Test | Description |
|------|-------------|
| `Authenticate_ValidCredentials_ReturnsNonEmptyToken` | Authenticates with valid credentials and asserts a non-empty API token is returned |
| `Authenticate_InvalidCredentials_ThrowsApiException` | Authenticates with bad credentials and asserts an `ApiException` is thrown |
| `GetSecretValuesAsync_StaticTextSecret_ReturnsDictWithValue` | Retrieves a `static_text` secret and asserts the response dictionary contains the secret path as a key with a non-empty value |
| `GetSecretValuesAsync_StaticKvSecret_ReturnsDictWithValue` | Retrieves a `static_kv` secret and asserts the response dictionary contains a non-empty value |
| `GetSecretValuesAsync_StaticJsonSecret_ReturnsDictWithValue` | Retrieves a `static_json` secret and asserts the response dictionary contains a non-empty value |
| `GetSecretValuesAsync_MultipleSecrets_ReturnsAllRequested` | Requests two secrets in a single API call and asserts both keys are present in the response |
| `GetSecretValuesAsync_InvalidToken_ThrowsApiException` | Calls `GetSecretValuesAsync` with an invalid token and asserts an `ApiException` is thrown |
| `Debug_K8sOrchestratorSecret_PrintsRawValue` | Retrieves the hardcoded `/pam/test/k8s-orchestrator` secret directly via the API client and asserts the response contains that key (the value itself is intentionally not written to output, to avoid secret exposure in CI logs) |
