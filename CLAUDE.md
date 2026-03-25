# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the **Akeyless PAM Provider** for Keyfactor Command — a C# class library that implements the `IPAMProvider` interface to retrieve secrets from Akeyless and provide them as credentials to Keyfactor Command and Universal Orchestrator extensions.

## Build Commands

```shell
# Build the PAM provider library
dotnet build akeyless-pam/akeyless-pam.csproj

# Build release (no debug symbols)
dotnet build akeyless-pam/akeyless-pam.csproj -c Release

# Build the test console
dotnet build TestConsole/TestConsole.csproj
```

## Tests

```shell
# Unit tests (no external dependencies, always runnable)
dotnet test tests/AkeylessPam.Unit.Tests/

# Integration tests (skip automatically when env vars absent)
dotnet test tests/AkeylessPam.Integration.Tests/

# Both test projects
dotnet test
```

Integration tests require env vars:
- `AKEYLESS_ACCESS_ID` / `AKEYLESS_ACCESS_KEY` — credentials (required for any integration test)
- `AKEYLESS_API_URL` — defaults to `https://api.akeyless.io`
- `AKEYLESS_AUTH_TYPE` — defaults to `access_key`
- `AKEYLESS_SECRET_STATIC_TEXT` / `AKEYLESS_SECRET_STATIC_KV` / `AKEYLESS_SECRET_STATIC_JSON` / `AKEYLESS_SECRET_STATIC_JSON_RAW` — paths to Akeyless secrets for each secret-type test

## Running the Test Console

The `TestConsole` project is a manual integration test harness — there are no automated unit tests. Configure environment variables, then run:

```shell
export AKEYLESS_API_URL="https://api.akeyless.io"
export AKEYLESS_AUTH_TYPE="access_key"
export AKEYLESS_ACCESS_ID="<your-access-id>"
export AKEYLESS_ACCESS_KEY="<your-access-key>"

dotnet run --project TestConsole/TestConsole.csproj
```

The test console exercises all three secret types (`static_text`, `static_kv`, `static_json`) against hardcoded secret paths under `pam/test/` in Akeyless.

## Architecture

The solution has two projects:

- **`akeyless-pam/`** — The PAM provider library (targets `net8.0`). This is what gets deployed to Keyfactor Command or Universal Orchestrator hosts.
- **`TestConsole/`** — A console app for manual end-to-end testing against a live Akeyless instance.

### Key Files

- `akeyless-pam/AkeylessPam.cs` — Main provider class implementing `IPAMProvider`. Entry point is `GetPassword()`, which builds config, authenticates, and retrieves the secret.
- `akeyless-pam/Models/AkeylessConfiguration.cs` — Configuration model with parameter key constants (used as dictionary keys when Keyfactor calls `GetPassword`), validation attributes, and supported types.
- `akeyless-pam/Constants.cs` — Default values (`access_key` auth, `https://api.akeyless.io`).
- `akeyless-pam/manifest.json` — Copied to output; used by Universal Orchestrators to configure the PAM provider.

### How It Works

Keyfactor Command calls `IPAMProvider.GetPassword(instanceParameters, serverConfigurationParameters)` with two dictionaries:

- **Server (initialization) parameters** — `Url`, `AuthType`, `AccessId`, `AccessKey` — set once per PAM provider instance in Command.
- **Instance parameters** — `SecretName`, `SecretType`, `StaticSecretFieldName` — set per Certificate Store or credential.

The provider authenticates to Akeyless using the `akeyless` NuGet SDK (`V2Api`), then retrieves the secret via `GetSecretValue`. Secret parsing depends on `SecretType`:
- `static_text` — returns raw string (auto-detects JSON and KV formats)
- `static_kv` — parses `key=value\n` lines, requires `StaticSecretFieldName`
- `static_json` — deserializes JSON, optionally extracts a field by `StaticSecretFieldName`

The `AKEYLESS_API_URL` environment variable overrides the configured URL at runtime.

### PAM Provider Registration

The provider is registered in Keyfactor Command by its fully qualified class name `Keyfactor.Extensions.Pam.Akeyless` and display name `Akeyless`. The `integration-manifest.json` at the repo root drives the Keyfactor CI/CD release pipeline (via the `keyfactor/actions` reusable workflow).

## Release

Releases are built from `akeyless-pam/bin/Release` (as defined in `integration-manifest.json`). The GitHub Actions workflow (`keyfactor-starter-workflow.yml`) handles building, signing, and publishing releases automatically on push/PR events.
