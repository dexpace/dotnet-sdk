# Security

Cross-cutting security rules for all languages and runtimes.

## Input Validation at Boundaries

- Validate **all** external input at system boundaries: user input, API responses, file content, environment variables, configuration files. Trust nothing from outside.
- Use schema validation libraries (Zod, Jackson `@JsonCreator` with `require()`, Go struct tags + `validator`). Parse, don't validate -- transform raw input into validated domain types or reject it.
- Validate at the boundary once, then pass validated types inward. Interior code trusts boundary types.

```
// Pseudocode -- boundary function parses raw input into validated type
fn parse_booking_request(raw: Map) -> BookingRequest | Error:
    assert raw["checkin"] is ISO date
    assert raw["checkout"] is ISO date
    assert raw["checkout"] > raw["checkin"]
    assert raw["guests"] in 1..20
    return BookingRequest(checkin, checkout, guests)
```

- Reject unknown fields at API boundaries. Fail closed, not open.
- Apply length limits to all string inputs. Apply range limits to all numeric inputs. No unbounded input.

## Injection Prevention

### SQL Injection

- **Parameterized queries only.** Never concatenate user input into SQL strings.
- No exceptions. No "it's just an internal tool." Parameterized queries always.

```
-- Good
SELECT * FROM hotels WHERE city = ?

-- Banned
SELECT * FROM hotels WHERE city = '" + userInput + "'"
```

### XSS

- Escape all output rendered in HTML. Use framework auto-escaping (React JSX, Thymeleaf, Go `html/template`).
- Set `Content-Security-Policy` headers. Start strict: `default-src 'self'`.
- Never inject user input into `innerHTML`, `dangerouslySetInnerHTML`, or template literals rendered as HTML.

### Command Injection

- Never shell out with user input. Never pass user input to `Runtime.exec()`, `os.exec()`, or equivalent subprocess APIs.
- If you must invoke external processes, use argument arrays (not shell strings) and validate every argument against an allowlist.

### Path Traversal

- Canonicalize all file paths before use. Reject paths containing `..`. Validate the resolved path starts with the expected base directory.

```
// Pseudocode
fn safe_resolve(base: Path, user_path: String) -> Path | Error:
    resolved = canonicalize(base / user_path)
    assert resolved.starts_with(canonicalize(base))
    return resolved
```

### Template Injection

- Never pass user input as template source. User input is data, never code. Use parameterized templates only.

## Secrets Management

- **Never hardcode secrets.** No API keys, passwords, tokens, or connection strings in source code.
- **Never log secrets.** Not at any log level. Not "temporarily for debugging."
- **Never commit secrets.** Use `.gitignore` for `.env` files. Run secret scanning in CI (`git-secrets`, `trufflehog`, `gitleaks`).
- Load secrets from environment variables or secret managers (AWS Secrets Manager, HashiCorp Vault, GCP Secret Manager) at runtime.
- Rotate secrets on a schedule. Assume any secret can be compromised.

## Credential Safety

- Every type holding a secret **must** override `toString()` to mask the value:

```kotlin
data class ApiCredential(val key: String, val secret: String) {
    override fun toString(): String = "ApiCredential(key=***, secret=***)"
}
```

```go
type Credential struct { Key, Secret string }
func (c Credential) String() string { return "Credential(***)" }
```

```python
@dataclass(frozen=True)
class ApiCredential:
    key: str
    secret: str

    def __repr__(self) -> str: return "ApiCredential(key=***, secret=***)"
    __str__ = __repr__
```

- Mask these headers in all log output: `Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `Proxy-Authorization`.
- Never log request/response bodies containing tokens, passwords, or secrets. Use field-level masking.

## Dependency Security

- **Audit dependencies.** Run vulnerability scanners in CI on every build:
  - JVM: `gradle dependencyCheck` (OWASP), `gradle dependencyUpdates`
  - Node: `npm audit`, `yarn audit`
  - Go: `govulncheck`
  - Python: `pip-audit`, `safety`
- **Pin versions.** Use lockfiles (`package-lock.json`, `gradle.lockfile`, `go.sum`). No floating version ranges for direct dependencies.
- **Zero tolerance** for known vulnerabilities in direct dependencies. Transitive vulnerabilities: assess and mitigate within one sprint.
- Review new dependencies before adding them. Prefer well-maintained, widely-used libraries. Every dependency is an attack surface.

## Cryptography

- **Never roll your own crypto.** Use platform standard libraries (`javax.crypto`, `crypto/subtle`, Web Crypto API).
- Hash passwords with **bcrypt** or **argon2**. Never MD5, SHA1, or plain SHA256 for passwords.
- Use **constant-time comparison** for secrets, tokens, and HMAC values. Never `==` or `.equals()` for secret comparison.

```kotlin
// Kotlin/Java -- constant time
MessageDigest.isEqual(expectedMac, actualMac)

// Banned -- timing side-channel
expectedMac.contentEquals(actualMac)
```

```go
// Go -- constant time
subtle.ConstantTimeCompare(expectedMac, actualMac) == 1

// Banned -- timing side-channel
bytes.Equal(expectedMac, actualMac)
```

```python
# Python -- constant time
secrets.compare_digest(expected_mac, actual_mac)

# Banned -- timing side-channel
expected_mac == actual_mac
```

- Generate random values with cryptographically secure PRNGs (`SecureRandom` on JVM, `crypto/rand` in Go, `secrets` / `os.urandom` in Python, `crypto.getRandomValues()` in JS). Never `Math.random()`, `random.random()`, or `rand()` for security-sensitive values.

## HTTPS / TLS

- **Enforce TLS everywhere.** All HTTP communication must use HTTPS. No exceptions for "internal" services.
- **Never disable certificate verification.** Not in tests, not in dev, not "temporarily." Use test certificates for test environments.
- Minimum **TLS 1.2**. Prefer TLS 1.3 where supported.
- Set `Strict-Transport-Security` headers on all responses.

## Rate Limiting and Abuse Prevention

- **Rate limit all public endpoints.** Use token bucket or sliding window algorithms. Return `429 Too Many Requests` with `Retry-After` header.
- **Set max request body sizes.** Reject payloads exceeding the limit before parsing.
- **Timeout everything.** Every external call (HTTP, database, DNS, file I/O) must have an explicit timeout. No default timeouts -- set them deliberately.
- Set connection limits per client. Set queue depths. Set maximum concurrent requests. Limits on everything (see root rules #9).
- Protect against slowloris and similar attacks with request-level timeouts, not just connection timeouts.

## Error Messages

- **Never leak internals in client-facing error responses.** No stack traces. No SQL queries. No file paths. No server versions. No dependency names.
- Return generic error codes and messages to clients. Log the full details server-side with a correlation ID.

```
// Client sees:
{ "error": "internal_error", "message": "An unexpected error occurred", "requestId": "abc-123" }

// Server logs:
ERROR [abc-123] NullPointerException at HotelService.java:42 ...
```

- Differentiate between client errors (4xx -- tell them what they did wrong) and server errors (5xx -- tell them nothing specific).
- Never confirm or deny existence of resources in auth errors. `401 Unauthorized` for both "user doesn't exist" and "wrong password."
