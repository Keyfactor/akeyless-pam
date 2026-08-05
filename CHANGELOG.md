# v1.1.0

## Features

- **Environment variable overrides for connection parameters** — `AuthType`, `AccessId`, and `AccessKey` can now be overridden at runtime via the `AKEYLESS_AUTH_TYPE`, `AKEYLESS_ACCESS_ID`, and `AKEYLESS_ACCESS_KEY` environment variables, respectively, matching the existing `AKEYLESS_API_URL` override for the Akeyless API URL. This lets deployments control Akeyless connection details at the infrastructure/deployment level (e.g. process environment, secrets injection) instead of only via `manifest.json` or the Command portal.
- Environment variable override values are trimmed of leading/trailing whitespace before use, so an incidental trailing newline (a common artifact of file-mounted/`envFrom` secret provisioning) no longer breaks the override.
- An `AKEYLESS_AUTH_TYPE` override is now validated against the supported auth types before use; an unrecognized value fails fast with `InvalidClientConfigurationException` instead of silently skipping authentication and proceeding with an unauthenticated request.
- The "Connecting to Akeyless at" debug log now reports the effective (post-override) URL rather than the pre-override configured value.
- An override value containing a non-printable or directionality-control character (surviving the leading/trailing trim — e.g. an embedded newline, ANSI escape sequence, U+2028/U+2029, or a Unicode bidirectional-override/zero-width character such as U+202E) is now rejected with `InvalidClientConfigurationException` instead of being trimmed only at the ends and passed through — none of the overridable parameters are legitimately multi-line or right-to-left, and letting one through risked forging extra lines or spoofed rendering in the log messages that echo the resolved value.
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