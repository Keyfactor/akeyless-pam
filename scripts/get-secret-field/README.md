# get-secret-field

Standalone scripts that reproduce, via raw REST calls, what the Akeyless PAM
Provider does for a `static_kv` (or `static_json`) secret: authenticate,
fetch the secret, then extract one or more fields from it client-side. Use
these to verify Akeyless connectivity and credentials before configuring the
PAM provider in Keyfactor Command — no build or deployment required.

Both scripts are equivalent — pick whichever fits your environment:

- `get-secret-field.ps1` (PowerShell)
- `get-secret-field.sh` (Bash)

## Shared setup

1. Copy the example env file and fill in your real values:

   ```
   cp get-secret-field.env.example get-secret-field.env
   ```

2. Edit `get-secret-field.env`:

   ```
   AKEYLESS_URL=https://api.akeyless.io
   AKEYLESS_ACCESS_ID=p-xxxxxxxx
   AKEYLESS_ACCESS_KEY=your-access-key
   AKEYLESS_SECRET_NAME=/path/to/your-kv-secret
   AKEYLESS_FIELD_NAMES=username,password
   ```

   `get-secret-field.env` is gitignored — it will never be committed.

## PowerShell

Requires PowerShell 7+ (`pwsh`) — works on Windows, macOS, and Linux.

```
pwsh -File ./get-secret-field.ps1
```

## Bash

Requires `bash`, `curl`, and `jq`.

```
./get-secret-field.sh
```

## Output

Each field in `AKEYLESS_FIELD_NAMES` is printed as `field = value`. Verbose
`[debug]` lines show every step (env file parsing, requests/responses,
format detection, field matching) to help troubleshoot connectivity or
parsing issues. If any requested field can't be found, the script exits
with a non-zero status and lists the missing field(s).

## Notes

- Values can also be supplied as real environment variables instead of the
  `.env` file — anything already set in the environment takes precedence
  over the file.
- Both scripts auto-detect whether the secret value is JSON-formatted
  (`{"key":"value"}`) or line-based (`key=value`), matching the PAM
  provider's own parsing logic.
