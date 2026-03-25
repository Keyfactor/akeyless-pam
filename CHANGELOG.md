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