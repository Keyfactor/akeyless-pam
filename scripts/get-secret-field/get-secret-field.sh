#!/usr/bin/env bash
# Reproduces, via raw REST calls, what the Akeyless PAM Provider does for a
# static_kv secret: authenticate with access_id/access_key, fetch the secret
# value, then extract one or more "key=value" fields from it client-side.
# Useful for verifying connectivity/credentials without deploying the provider.
#
# Configuration is read from environment variables, which can be supplied via
# a "get-secret-field.env" file (KEY=value, one per line, "#" comments
# allowed) placed alongside this script. Copy get-secret-field.env.example to
# get-secret-field.env and fill in real values — that file is gitignored.
# Variables already present in the environment are never overwritten by the
# env file. If nothing is set, the placeholder defaults below are used.
#
# Requires: curl, jq
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/get-secret-field.env}"

command -v jq >/dev/null 2>&1 || { echo "jq is required but not installed." >&2; exit 1; }

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

echo "[debug] EnvFile path: $ENV_FILE" >&2
if [[ -f "$ENV_FILE" ]]; then
  echo "[debug] EnvFile found; parsing..." >&2
  line_number=0
  while IFS= read -r line || [[ -n "$line" ]]; do
    line_number=$((line_number + 1))
    trimmed="$(trim "$line")"
    if [[ -z "$trimmed" ]]; then
      echo "[debug]   line ${line_number}: blank, skipping" >&2
      continue
    fi
    if [[ "$trimmed" == \#* ]]; then
      echo "[debug]   line ${line_number}: comment, skipping" >&2
      continue
    fi
    if [[ "$trimmed" == export\ * ]]; then
      trimmed="$(trim "${trimmed#export }")"
      echo "[debug]   line ${line_number}: stripped leading 'export '" >&2
    fi

    if [[ "$trimmed" != *=* ]]; then
      echo "[debug]   line ${line_number}: no '=' found, skipping" >&2
      continue
    fi

    key="$(trim "${trimmed%%=*}")"
    value="$(trim "${trimmed#*=}")"
    value="${value%\"}"
    value="${value#\"}"

    if [[ -z "${!key:-}" ]]; then
      export "$key=$value"
      echo "[debug]   line ${line_number}: set $key = $value" >&2
    else
      echo "[debug]   line ${line_number}: $key already set in environment, not overwriting" >&2
    fi
  done < "$ENV_FILE"
else
  echo "[debug] EnvFile not found; using process environment / placeholder defaults" >&2
fi

# ==== Configuration - override via env vars or get-secret-field.env ====
AKEYLESS_URL="${AKEYLESS_URL:-https://api.akeyless.io}"
ACCESS_ID="${AKEYLESS_ACCESS_ID:-p-xxxxxxxx}"
ACCESS_KEY="${AKEYLESS_ACCESS_KEY:-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx}"
SECRET_NAME="${AKEYLESS_SECRET_NAME:-/path/to/my-kv-secret}"
FIELD_NAMES_RAW="${AKEYLESS_FIELD_NAMES:-username,password}"
# =========================================================================

echo "[debug] Resolved configuration:" >&2
echo "[debug]   AkeylessUrl = $AKEYLESS_URL" >&2
echo "[debug]   AccessId    = $ACCESS_ID" >&2
echo "[debug]   AccessKey   = $ACCESS_KEY" >&2
echo "[debug]   SecretName  = $SECRET_NAME" >&2
echo "[debug]   FieldNames  = $FIELD_NAMES_RAW" >&2

echo "Authenticating to Akeyless at ${AKEYLESS_URL}..." >&2
auth_body="$(jq -n --arg id "$ACCESS_ID" --arg key "$ACCESS_KEY" '{"access-id":$id,"access-key":$key}')"
echo "[debug] Auth request body: $auth_body" >&2
auth_response=$(curl -sS -X POST "${AKEYLESS_URL}/auth" \
  -H "Content-Type: application/json" \
  -d "$auth_body")
echo "[debug] Auth response: $auth_response" >&2

token=$(echo "$auth_response" | jq -r '.token // empty')
if [[ -z "$token" ]]; then
  echo "Authentication failed. Response: $auth_response" >&2
  exit 1
fi
echo "[debug] Token = $token" >&2

echo "Fetching secret '${SECRET_NAME}'..." >&2
secret_body="$(jq -n --arg name "$SECRET_NAME" --arg token "$token" '{"names":[$name],"token":$token}')"
echo "[debug] Secret request body: $secret_body" >&2
secret_response=$(curl -sS -X POST "${AKEYLESS_URL}/get-secret-value" \
  -H "Content-Type: application/json" \
  -d "$secret_body")
echo "[debug] Secret response: $secret_response" >&2

secret_value=$(echo "$secret_response" | jq -r --arg name "$SECRET_NAME" '.[$name] // empty')
if [[ -z "$secret_value" ]]; then
  echo "Secret '${SECRET_NAME}' not found or empty. Response: $secret_response" >&2
  exit 1
fi
echo "[debug] Raw secret value: $secret_value" >&2

# Mirrors AkeylessPam.cs: static_kv/static_json secrets are typically stored as a JSON
# object in Akeyless, but fall back to "key=value" line parsing if not JSON-shaped.
trimmed_value="$(trim "$secret_value")"
looks_like_json=false
if [[ "$trimmed_value" == \{*\} || "$trimmed_value" == \[*\] ]]; then
  looks_like_json=true
fi
echo "[debug] LooksLikeJson = $looks_like_json" >&2

if [[ "$looks_like_json" == true ]]; then
  echo "[debug] Parsed JSON properties: $(echo "$secret_value" | jq -r 'keys | join(", ")')" >&2
else
  echo "[debug] Treating as KV lines:" >&2
  kv_line_number=0
  while IFS= read -r kv_line || [[ -n "$kv_line" ]]; do
    kv_line_number=$((kv_line_number + 1))
    echo "[debug]   line ${kv_line_number}: '$kv_line'" >&2
  done <<< "$secret_value"
fi

get_field_value() {
  local field_name="$1"
  if [[ "$looks_like_json" == true ]]; then
    local value
    value=$(echo "$secret_value" | jq -r --arg name "$field_name" '.[$name] // empty')
    if [[ -n "$value" ]]; then
      echo "[debug] Field '$field_name' found via JSON property: $value" >&2
      printf '%s' "$value"
      return 0
    fi
    echo "[debug] Field '$field_name' not found among JSON properties" >&2
    return 1
  fi

  local key value
  while IFS='=' read -r key value; do
    key="$(trim "$key")"
    if [[ "$key" == "$field_name" ]]; then
      echo "[debug] Field '$field_name' matched KV line: '$key=$value'" >&2
      printf '%s' "$(trim "$value")"
      return 0
    fi
  done <<< "$secret_value"
  echo "[debug] Field '$field_name' not found in any KV line" >&2
  return 1
}

missing_fields=()
IFS=',' read -ra field_names <<< "$FIELD_NAMES_RAW"
for raw_field_name in "${field_names[@]}"; do
  field_name="$(trim "$raw_field_name")"
  [[ -z "$field_name" ]] && continue
  echo "[debug] Looking up field '$field_name'..." >&2
  if field_value=$(get_field_value "$field_name"); then
    echo "${field_name} = ${field_value}"
  else
    missing_fields+=("$field_name")
  fi
done

if [[ ${#missing_fields[@]} -gt 0 ]]; then
  joined=$(IFS=', '; echo "${missing_fields[*]}")
  echo "Field(s) not found in secret '${SECRET_NAME}': ${joined}" >&2
  exit 1
fi
