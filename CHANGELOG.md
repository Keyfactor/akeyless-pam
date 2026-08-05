# v1.1.0

## Features

- **Environment variable overrides for connection parameters** — `AuthType`, `AccessId`, and `AccessKey` can now be overridden at runtime via the `AKEYLESS_AUTH_TYPE`, `AKEYLESS_ACCESS_ID`, and `AKEYLESS_ACCESS_KEY` environment variables, respectively, matching the existing `AKEYLESS_API_URL` override for the Akeyless API URL. This lets deployments control Akeyless connection details at the infrastructure/deployment level (e.g. process environment, secrets injection) instead of only via `manifest.json` or the Command portal.
- Environment variable override values are trimmed of leading/trailing whitespace before use, so an incidental trailing newline (a common artifact of file-mounted/`envFrom` secret provisioning) no longer breaks the override.
- An `AKEYLESS_AUTH_TYPE` override is now validated against the supported auth types before use; an unrecognized value fails fast with `InvalidClientConfigurationException` instead of silently skipping authentication and proceeding with an unauthenticated request.
- The "Connecting to Akeyless at" debug log now reports the effective (post-override) URL rather than the pre-override configured value.
- `Url`, `AuthType`, `AccessId`, and `AccessKey` are now required to be printable ASCII, whether the effective value came from an env var override or from Command's/`manifest.json`'s configuration — a value containing anything else (an embedded newline, ANSI escape sequence, Unicode line/paragraph separator, bidirectional-override or zero-width character, etc.) is rejected with `InvalidClientConfigurationException` instead of being passed through. None of these parameters are legitimately anything but printable ASCII, and letting a non-ASCII value through risked forging extra lines or spoofed rendering in the log messages that echo the resolved value. This check runs both where the configured value is first read (before `BuildAkeylessConfiguration`'s own debug logging echoes it) and again on the final resolved value in `InitClient` (which may instead be an env var override), so a malicious/malformed value can't reach either log statement unvalidated.
- A failed Akeyless authentication attempt (`ApiException`) now logs the `AccessId` that was used, matching the existing success/empty-token log lines — previously this was the one authentication-failure path with no record of which identity (configured or env-overridden) the failed attempt used.
- The `ApiException` catch in `InitClient` no longer passes the exception object itself to the logger — only the HTTP status code and `AccessId` are logged. Passing the exception object defeated the log call's own stated intent to exclude `ex.Message` (which may echo back the raw Akeyless response body, including credentials), since most logging providers render an attached exception's `Message`/`ToString()` regardless of the message template.

# v1.0.0

Initial release of the Akeyless PAM Provider for Keyfactor Command and Universal Orchestrator.

### Features

- Retrieve secrets from Akeyless and surface them as credentials to Keyfactor Command certificate stores and orchestrator jobs
- **Access Key (API Key) authentication** — authenticates to the Akeyless API using an Access ID and Access Key pair
- **Static Text secrets** (`static_text`) — returns the secret value as a plain string
- **Static JSON secrets** (`static_json`) — returns the full JSON blob, or extracts a single field by name via `StaticSecretFieldName`
- **Static Key-Value secrets** (`static_kv`) — extracts a single field from a key-value secret by name via `StaticSecretFieldName`
- Configurable Akeyless API URL (defaults to `https://api.akeyless.io`); can be overridden at runtime via the `AKEYLESS_API_URL` environment variable
- Targets .NET 8.0 and .NET 10.0