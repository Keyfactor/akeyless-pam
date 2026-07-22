# v1.1.0

## Features

- **Environment variable overrides for connection parameters** — `AuthType`, `AccessId`, and `AccessKey` can now be overridden at runtime via the `AKEYLESS_AUTH_TYPE`, `AKEYLESS_ACCESS_ID`, and `AKEYLESS_ACCESS_KEY` environment variables, respectively, matching the existing `AKEYLESS_API_URL` override for the Akeyless API URL. This lets deployments control Akeyless connection details at the infrastructure/deployment level (e.g. process environment, secrets injection) instead of only via `manifest.json` or the Command portal.

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