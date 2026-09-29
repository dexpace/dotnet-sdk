# Security Policy

## Supported versions

Nothing has been published yet. The three packages under `src/` are built, but every one is at
`0.0.1-alpha.1` and none is on NuGet, so there is no released version to support and no patched
release to point at. Until the first release, the supported revision is the tip of `main` — report
against a commit SHA. Once a version is published, this section will name the supported release lines.

The defects fixed before any release (roadmap phase 1, S1–S9) are listed in `CHANGELOG.md` under
`[Unreleased]` → `Security`. Because no consumer could hold an affected version, no advisory was
issued for them; that decision is recorded in the
[phase 1 checklist](docs/work/mvp/phase1/2026-09-28-phase1-security-fixes-checklist.md#security-advisory-decision).
From the first published version onward, a vulnerability gets a GitHub security advisory.

## Reporting a vulnerability

Please **do not** open a public issue, pull request or discussion for a security vulnerability.

Report it privately by email to
[oaljarrah@dexpace.org](mailto:oaljarrah@dexpace.org) with `[SECURITY]` in
the subject line.

Include what you can of the following:

- The affected package(s), and the commit SHA and .NET SDK / runtime version you reproduced against
- A description of the vulnerability and its impact
- Steps or a proof of concept to reproduce it

You can expect an acknowledgement within a few days. Please allow time for
a fix to land and be released before disclosing publicly.

## Scope

The SDK is a **toolkit**: `Dexpace.Sdk.Core` performs no network I/O of its own. In scope here:

- **Credential handling** — the API-key, Basic and bearer-token credentials and auth policies, the
  access-token cache, the HTTPS-only guard on credentials, and any path by which a credential reaches a
  log, a trace, an exception message or `ToString()` output.
- **Redaction** — URL, query and header redaction in logging and tracing, including a credential
  surviving in userinfo or in a query parameter outside the allow-list.
- **Redirect safety** — `Authorization` stripped before every re-issue, `Cookie` and
  `Proxy-Authorization` stripped cross-origin, scheme downgrades, and the SDK remaining the only
  redirect authority over the transport.
- **Request integrity** — header-name and header-value validation (CR/LF injection, request
  splitting), the framing headers the transport drops, and URL handling.
- **Resource exhaustion** — the SDK's own bounded buffers, retry delays and pagination caps.
- **The transport adapter** — `Dexpace.Sdk.Http.SystemNet`, as far as its own mapping, redirect
  handling and cancellation go — and the `Dexpace.Sdk.Serialization.SystemTextJson` codec.

Vulnerabilities in the .NET runtime, `System.Net.Http`, `System.Text.Json` or
`Microsoft.Extensions.Logging.Abstractions` themselves belong upstream; report them there, and tell us
if the SDK needs a dependency floor raised.
