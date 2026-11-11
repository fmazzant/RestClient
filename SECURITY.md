# Security Policy

## Supported Versions

| Version | Supported |
| ------- | --------- |
| 3.0.x (`Mafesoft.RestClient*`) | :white_check_mark: |
| < 3.0 (`Mafe.RestClient*`) | :x: |

The `Mafe.RestClient*` packages are deprecated: migrate to `Mafesoft.RestClient*`
(see [Migrating to 3.0](README.md#migrating-to-30)). Do not use the 1.x versions in particular: on .NET Framework/.NET
Standard they disabled the TLS certificate validation for the whole process.

## Reporting a Vulnerability

Please **do not open a public issue** for a security problem.

Report it privately with [GitHub private vulnerability reporting](https://github.com/fmazzant/RestClient/security/advisories/new)
(**Security** tab → **Report a vulnerability**), describing:

- the affected package and version;
- how to reproduce the problem;
- the impact you expect.

You will receive an answer as soon as possible. When the problem is confirmed, a fixed version is released and
the advisory is published with credit to the reporter, unless you prefer to stay anonymous.
