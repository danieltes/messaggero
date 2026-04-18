# Security Policy

## Supported Versions

Security fixes are generally provided only for the last minor version. Security fixes are released either as part of the next minor version or as an on-demand patch version.

Security fixes are given priority and might be enough to cause a new version to be released.

Please upgrade to the latest published release on [NuGet](https://www.nuget.org/packages/Messaggero).

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Use GitHub's private disclosure feature instead:

1. Go to the [Security tab](../../security) of this repository.
2. Click **"Report a vulnerability"**.
3. Fill in the details (description, reproduction steps, affected versions, potential impact).

We will try to acknowledge within **10 business days**. We aim to triage and produce a fix within **30 days** of a confirmed report, coordinating a public disclosure (CVE if applicable) only after a patched release is available.

## Out of Scope

The following are **not** considered security vulnerabilities:

- Security issues in the underlying broker infrastructure (Kafka, RabbitMQ) — report those to the respective projects
- Misconfiguration of credentials or TLS in the host application's own setup
- Missing features, routing API design feedback, or handler lifecycle behavior
- Performance characteristics or throughput degradation
- Issues only reproducible in unsupported versions

Please open a regular [GitHub issue](../../issues) for those.
