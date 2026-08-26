<#
.SYNOPSIS
Reproduces, via raw REST calls, what the Akeyless PAM Provider does for a
static_kv secret: authenticate with access_id/access_key, fetch the secret
value, then extract one or more "key=value" fields from it client-side.
Useful for verifying connectivity/credentials without deploying the provider.

.DESCRIPTION
Configuration is read from environment variables, which can be supplied via
a "get-secret-field.env" file (KEY=value, one per line, "#" comments allowed)
placed alongside this script. Copy get-secret-field.env.example to
get-secret-field.env and fill in real values — that file is gitignored.
Variables already present in the environment are never overwritten by the
env file. If nothing is set, the placeholder defaults below are used.
#>

param(
    [string]$EnvFile = (Join-Path $PSScriptRoot "get-secret-field.env")
)

$ErrorActionPreference = "Stop"

Write-Host "[debug] EnvFile path: $EnvFile"
if (Test-Path $EnvFile) {
    Write-Host "[debug] EnvFile found; parsing..."
    $lineNumber = 0
    foreach ($line in Get-Content $EnvFile) {
        $lineNumber++
        $trimmed = $line.Trim()
        if ([string]::IsNullOrEmpty($trimmed)) {
            Write-Host "[debug]   line ${lineNumber}: blank, skipping"
            continue
        }
        if ($trimmed.StartsWith("#")) {
            Write-Host "[debug]   line ${lineNumber}: comment, skipping"
            continue
        }
        if ($trimmed.StartsWith("export ", [StringComparison]::OrdinalIgnoreCase)) {
            $trimmed = $trimmed.Substring(7).TrimStart()
            Write-Host "[debug]   line ${lineNumber}: stripped leading 'export '"
        }

        $eq = $trimmed.IndexOf("=")
        if ($eq -le 0) {
            Write-Host "[debug]   line ${lineNumber}: no '=' found, skipping"
            continue
        }

        $key = $trimmed.Substring(0, $eq).Trim()
        $value = $trimmed.Substring($eq + 1).Trim().Trim('"')

        if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($key))) {
            [Environment]::SetEnvironmentVariable($key, $value)
            Write-Host "[debug]   line ${lineNumber}: set $key = $value"
        } else {
            Write-Host "[debug]   line ${lineNumber}: $key already set in environment, not overwriting"
        }
    }
} else {
    Write-Host "[debug] EnvFile not found; using process environment / placeholder defaults"
}

# ==== Configuration - override via env vars or get-secret-field.env ====
$AkeylessUrl = if ($env:AKEYLESS_URL) { $env:AKEYLESS_URL } else { "https://api.akeyless.io" }
$AccessId    = if ($env:AKEYLESS_ACCESS_ID) { $env:AKEYLESS_ACCESS_ID } else { "p-xxxxxxxx" }
$AccessKey   = if ($env:AKEYLESS_ACCESS_KEY) { $env:AKEYLESS_ACCESS_KEY } else { "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" }
$SecretName  = if ($env:AKEYLESS_SECRET_NAME) { $env:AKEYLESS_SECRET_NAME } else { "/path/to/my-kv-secret" }
$FieldNamesRaw = if ($env:AKEYLESS_FIELD_NAMES) { $env:AKEYLESS_FIELD_NAMES } else { "username,password" }
$FieldNames = $FieldNamesRaw -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ }
# =========================================================================

Write-Host "[debug] Resolved configuration:"
Write-Host "[debug]   AkeylessUrl = $AkeylessUrl"
Write-Host "[debug]   AccessId    = $AccessId"
Write-Host "[debug]   AccessKey   = $AccessKey"
Write-Host "[debug]   SecretName  = $SecretName"
Write-Host "[debug]   FieldNames  = $($FieldNames -join ', ')"

Write-Host "Authenticating to Akeyless at $AkeylessUrl..."
$authBody = @{ "access-id" = $AccessId; "access-key" = $AccessKey } | ConvertTo-Json
Write-Host "[debug] Auth request body: $authBody"
$authResponse = Invoke-RestMethod -Method Post -Uri "$AkeylessUrl/auth" -ContentType "application/json" -Body $authBody
Write-Host "[debug] Auth response: $($authResponse | ConvertTo-Json -Compress)"

$token = $authResponse.token
if ([string]::IsNullOrEmpty($token)) {
    Write-Error "Authentication failed. Response: $($authResponse | ConvertTo-Json -Compress)"
    exit 1
}
Write-Host "[debug] Token = $token"

Write-Host "Fetching secret '$SecretName'..."
$secretBody = @{ names = @($SecretName); token = $token } | ConvertTo-Json
Write-Host "[debug] Secret request body: $secretBody"
$secretResponse = Invoke-RestMethod -Method Post -Uri "$AkeylessUrl/get-secret-value" -ContentType "application/json" -Body $secretBody
Write-Host "[debug] Secret response: $($secretResponse | ConvertTo-Json -Compress)"

$secretProperty = $secretResponse.PSObject.Properties[$SecretName]
if ($null -eq $secretProperty -or [string]::IsNullOrEmpty($secretProperty.Value)) {
    Write-Error "Secret '$SecretName' not found or empty."
    exit 1
}
$secretValue = $secretProperty.Value
Write-Host "[debug] Raw secret value: $secretValue"

# Mirrors AkeylessPam.cs: static_kv/static_json secrets are typically stored as a JSON
# object in Akeyless, but fall back to "key=value" line parsing if not JSON-shaped.
$trimmedValue = $secretValue.Trim()
$looksLikeJson = ($trimmedValue.StartsWith("{") -and $trimmedValue.EndsWith("}")) -or
                 ($trimmedValue.StartsWith("[") -and $trimmedValue.EndsWith("]"))
Write-Host "[debug] LooksLikeJson = $looksLikeJson"
$jsonObj = if ($looksLikeJson) { $secretValue | ConvertFrom-Json } else { $null }
if ($null -ne $jsonObj) {
    Write-Host "[debug] Parsed JSON properties: $(($jsonObj.PSObject.Properties | ForEach-Object { $_.Name }) -join ', ')"
} else {
    $kvLines = $secretValue -split "`n"
    Write-Host "[debug] Treating as KV lines (count: $($kvLines.Count)):"
    for ($i = 0; $i -lt $kvLines.Count; $i++) {
        Write-Host "[debug]   line $($i + 1): '$($kvLines[$i])'"
    }
}

function Get-FieldValue([string]$fieldName) {
    if ($null -ne $jsonObj) {
        $prop = $jsonObj.PSObject.Properties[$fieldName]
        if ($null -ne $prop) {
            Write-Host "[debug] Field '$fieldName' found via JSON property: $($prop.Value)"
            return "$($prop.Value)"
        }
        Write-Host "[debug] Field '$fieldName' not found among JSON properties"
        return $null
    }

    foreach ($line in $secretValue -split "`n") {
        $parts = $line.Split("=", 2)
        if ($parts.Length -eq 2 -and $parts[0].Trim() -eq $fieldName) {
            Write-Host "[debug] Field '$fieldName' matched KV line: '$line'"
            return $parts[1].Trim()
        }
    }
    Write-Host "[debug] Field '$fieldName' not found in any KV line"
    return $null
}

$missingFields = @()
foreach ($fieldName in $FieldNames) {
    Write-Host "[debug] Looking up field '$fieldName'..."
    $fieldValue = Get-FieldValue $fieldName
    if ([string]::IsNullOrEmpty($fieldValue)) {
        $missingFields += $fieldName
        continue
    }
    Write-Output "$fieldName = $fieldValue"
}

if ($missingFields.Count -gt 0) {
    Write-Error "Field(s) not found in secret '$SecretName': $($missingFields -join ', ')"
    exit 1
}
