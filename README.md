<h1 align="center" style="border-bottom: none">
    Akeyless PAM Provider
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/akeyless-pam/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/akeyless-pam?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/akeyless-pam?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/akeyless-pam/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <a href="#support"><b>Support</b></a> ·
  <a href="#getting-started"><b>Installation</b></a> ·
  <a href="#license"><b>License</b></a> ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=pam"><b>Related Integrations</b></a>
</p>

## Overview

The Akeyless PAM Provider allows for the retrieval of stored account credentials from an Akeyless secret.

## Support
The Akeyless PAM Provider is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com.

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Getting Started

The Akeyless PAM Provider is used by Command to resolve PAM-eligible credentials for Universal Orchestrator extensions and for accessing Certificate Authorities.

### Installation


#### Requirements

- Akeyless credentials w/ permission to access the secret(s) being used. See the [Akeyless documentation](https://docs.akeyless.io/reference/auth) for more information on how to configure the different types of auth.

#### Create PAM type in Keyfactor Command

##### Using `kfutil`
```shell
# Akeyless
kfutil pam types-create -r akeyless-pam -n Akeyless
```

##### Using the API
```json
{
  "Name": "Akeyless",
  "Parameters": [
    {
      "Name": "Url",
      "DisplayName": "Akeyless URL",
      "Description": "The URL to the Akeyless instance. Defaults to: https://api.akeyless.io",
      "DataType": 1,
      "InstanceLevel": false
    },
    {
      "Name": "AccessKeyId",
      "DisplayName": "Access Key ID",
      "Description": "The access key ID used to authenticate to Akeyless using \u0060access_key\u0060 authentication.",
      "DataType": 2,
      "InstanceLevel": false
    },
    {
      "Name": "AccessKey",
      "DisplayName": "Access Key",
      "Description": "The access key used to authenticate to Akeyless using \u0060access_key\u0060 authentication.",
      "DataType": 2,
      "InstanceLevel": false
    },
    {
      "Name": "AuthType",
      "DisplayName": "Auth Type",
      "Description": "The auth type used to authenticate to the Akeyless platform. Supported types are \u0060access_key\u0060.",
      "DataType": 1,
      "InstanceLevel": false
    },
    {
      "Name": "SecretName",
      "DisplayName": "Secret Name",
      "Description": "The full name (path) of the secret in Akeyless that contains the credential to retrieve.",
      "DataType": 1,
      "InstanceLevel": true
    },
    {
      "Name": "SecretType",
      "DisplayName": "Secret Type",
      "Description": "The type of secret stored in Akeyless. Supported types are \u0060static_kv,static_text,static_json\u0060.",
      "DataType": 1,
      "InstanceLevel": true
    },
    {
      "Name": "StaticSecretFieldName",
      "DisplayName": "Static Secret Field Name",
      "Description": "The field name within a static secret to retrieve the credential from. Required for \u0060static_kv\u0060 and optional for \u0060static_json\u0060 secret types.",
      "DataType": 1,
      "InstanceLevel": true
    }
  ]
}
```

#### Install PAM provider on Keyfactor Command Host (Local)

1. On the server that hosts Keyfactor Command, download and unzip the latest release of the Akeyless PAM Provider from the [Releases](../../releases) page.

2. Copy the assemblies to the appropriate directories on the Keyfactor Command server.

3. Restart the Keyfactor Command services (`iisreset`).

#### Install PAM provider on a Universal Orchestrator Host (Remote)

1. Install the Akeyless PAM Provider assemblies using kfutil or manually from the [Releases](../../releases) page.

2. Included in the release is a `manifest.json` file. Populate with credentials from the [requirements](docs/akeyless.md#requirements) section.

3. Restart the Universal Orchestrator service.


### Usage


#### From Keyfactor Command Host (Local)

| Initialization parameter | Display Name | Description |
| --- | --- | --- |
| Url | Akeyless URL | The URL to the Akeyless instance. Defaults to: https://api.akeyless.io |
| AccessKeyId | Access Key ID | The access key ID used to authenticate to Akeyless using `access_key` authentication. |
| AccessKey | Access Key | The access key used to authenticate to Akeyless using `access_key` authentication. |
| AuthType | Auth Type | The auth type used to authenticate to the Akeyless platform. Supported types are `access_key`. |

#### From a Universal Orchestrator Host (Remote)

| Instance parameter | Display Name | Description |
| --- | --- | --- |
| SecretName | Secret Name | The full name (path) of the secret in Akeyless that contains the credential to retrieve. |
| SecretType | Secret Type | The type of secret stored in Akeyless. Supported types are `static_kv,static_text,static_json`. |
| StaticSecretFieldName | Static Secret Field Name | The field name within a static secret to retrieve the credential from. Required for `static_kv` and optional for `static_json` secret types. |

> [!NOTE]
> Additional information on Akeyless can be found in the [supplemental documentation](docs/akeyless.md).

## License

Apache License 2.0, see [LICENSE](LICENSE)

## Related Integrations

See all [Keyfactor PAM Provider extensions](https://github.com/orgs/Keyfactor/repositories?q=pam).
