# Configuration

**As built by phase 5a, written against source on 2026-10-07** (branch `64-phase-5a-configuration`). This page describes the
configuration surface of `Dexpace.Sdk.Core` after phase 5a: the options records, the clock and its waits, `HttpDate`,
`ProxyOptions`, and `BuildInfo`. The requirement IDs it cites are the normative text
(`docs/product-spec/16-configuration.md`, `CFG-8`-`CFG-36`); the decisions behind each type are in the
[phase 5a design](../work/mvp/phase5/phase5a/2026-10-07-phase5a-configuration-design.md) and in
[design §8.2 and §8.3](../sdk-design-dotnet/08-instrumentation-and-configuration.md). Neither is restated here. The layered lookup
(`IConfiguration`, environment, `appsettings.json`) is phase 9's binding tier and is not built.

Ruby's counterpart page (`ruby-sdk/docs/sdk-documentation/`) is not in the local clone, as the earlier pages also found; Node's `config/`
tests were read from `nodejs-sdk@54aeed4`. This page is written from this repository's source.

## The options records

`DexpaceClientOptions`, `RetryOptions` and `RedirectOptions` are sealed records whose members are `init`-only (`CFG-8`). Set them in an
object initializer; derive a modified copy with `with`, which leaves the source unchanged (`CFG-9`):

```csharp
var options = new DexpaceClientOptions { OverallTimeout = TimeSpan.FromSeconds(30) };
var patient = options with { Retry = options.Retry with { MaxRetryAttempts = 5 } };
```

- **Nesting.** `Retry` and `Redirect` are non-null records; assigning `null` throws `ArgumentNullException`, as does a `null` `UserAgent`
  (a blank one is allowed and means "send no header").
- **Validation at `init`.** `BaseAddress` must be an absolute `http` or `https` URI with no fragment; otherwise an `ArgumentException`
  naming the redacted address is thrown where it is set, not at `BuildRequest`. Numeric and cross-property rules (a negative retry count,
  `BaseDelay` above `MaxDelay`) were deliberately **not** checked in 5a. *Correction, 2026-10-08 (phase 6a):* `RetryOptions` now validates every
  member where it is set, and `OverallTimeout` / `AttemptTimeout` accept `null` or `(0, 49 days]`; `BaseDelay` above `MaxDelay` stays
  legal (the cap wins). See [`retry.md`](./retry.md). Phases 6b and 9 own the other options.
- **`ToString`** renders every member with `BaseAddress` through `UrlRedactor`, so a signed query never reaches a log.
- **Per call.** `HttpPipeline.SendAsync` and `Send` overloads taking a `DexpaceClientOptions` run one call with that value;
  `RequestOptions` remains the transport seam's per-call carrier (timeout, retry cap, tags).
- There is no configuration builder (`CFG-12` is N/A) and no process-wide configuration slot (`CFG-13`).

## The clock and the waits

`TimeProvider` is the clock seam. `TimeProviderWaits` adds the two waits it lacks (`CFG-15`, `CFG-17`, `CFG-18`):

```csharp
timeProvider.Sleep(TimeSpan.FromSeconds(2), cancellationToken);          // blocks the calling thread, interruptible
await timeProvider.DelayAsync(TimeSpan.FromSeconds(2), cancellationToken); // a timer, no thread held
```

A negative delay, `Timeout.InfiniteTimeSpan` included, throws `ArgumentOutOfRangeException` (the BCL treats `-1 ms` as "wait forever").
Zero returns at once without arming a timer; an already-signalled token throws before any timer; a delay above 49 days runs as
successive bounded waits. Inside `src/`, `Thread.Sleep`, every `Task.Delay`, `DateTime`/`DateTimeOffset` `Now`/`UtcNow`/`Today` and the
`Environment` variable readers are banned (`RS0030`), with one sanctioned site each for `Task.Delay` and the environment read.
Elapsed time is measured through `TimeProvider.GetTimestamp`. A result produced after a caller stopped waiting is disposed by the
internal `LateResult` helper (`CFG-21`); `CFG-20`'s interruptible-task future is retired (design §10 entry 8).

## HTTP dates

`HttpDate.Format` renders RFC 1123 in UTC with a zero-padded day and a literal `GMT` (`CFG-29`). `HttpDate.TryParse` and `Parse` accept a
one- or two-digit day, a case-insensitive month, the zones `GMT`, `UTC` (any case), `+0000` and `+00:00`, and ignore the weekday
(`CFG-30`). Blank input, a missing comma after the weekday, RFC 850 and asctime are rejected (`CFG-31`); `Parse` throws a
`FormatException` that never echoes the input. `RetryPolicy`, `SetDatePolicy` and `RequestConditions` use it.

## Proxies

`ProxyOptions` is the transport-facing proxy model: `Type` (`Http`, `Socks4`, `Socks5`), `Host`, `Port`, an ordered `NonProxyHosts` glob
list, `UserName` and `Password`, an `ICredentials?` `ChallengeCredentials` slot, and `BypassAll`. It is **not** a property of
`DexpaceClientOptions`, and installing it in a transport is phase 8b's job. `ToString` prints `***` for a present credential
(`CFG-22`). `IsBypassed(host)` matches the globs `*` (any run) and `?` (one character), full-string and case-insensitive, with every
other character literal (`CFG-23`). Write `*.internal.example.com`: a leading dot is a **literal**, unlike curl and
`HttpClient.DefaultProxy`.

`ProxyOptions.FromEnvironment` (three overloads; the `Func<string, string?>` one is the hermetic seam, usable as
`key => configuration[key]`) resolves a proxy, never throws for its input, and warns without echoing the value (`CFG-24`-`CFG-27`):

- the variables are `HTTPS_PROXY`, `https_proxy`, `HTTP_PROXY`, `http_proxy`, the first present non-empty one winning, and one proxy
  serves every target; upper-case `HTTP_PROXY` is skipped when `GATEWAY_INTERFACE` is set (httpoxy), and `http_proxy` with it when a case-insensitive lookup returns the same value;
- a malformed chosen value yields `null` and one warning (`ProxyConfigurationIgnored`, provisional event id 20); it never falls through;
- the URL is `scheme://[user[:password]@]host:port[/]` with `http`, `socks4`, `socks4a`, `socks5` or `socks5h`; `https` is rejected, the
  port must be explicit, all digits and 0 to 65535, and a malformed percent escape in the credentials is invalid;
- `NO_PROXY` (then `no_proxy`) splits on unescaped commas; exactly `*` returns `null` (route directly).

Nothing in core calls `FromEnvironment` (`CFG-28`).

## Build identity

`BuildInfo` exposes `SdkVersion`, `RuntimeVersion` (`Environment.Version`), `RuntimeDescription`, `OSName` and `IdentityTokens`
(`dexpace-dotnet/<version>`, `dotnet/<runtime>`), resolved once, each falling back to `unknown` and each token header-safe (`CFG-36`).
The default `User-Agent` is the tokens joined by a space.

## What is not configurable, and why

The 64 MiB materialisation cap is a constant; its configurable form is phase 7a's per-read limit, and the SSE line cap is 7b's. The
context-store capacity is a constant (a process-wide store has no coherent per-client option). The binding tier is phase 9.

## Migration from the mutable options

| Before | After |
|---|---|
| `options.Retry.MaxRetryAttempts = 5;` | `options = options with { Retry = options.Retry with { MaxRetryAttempts = 5 } };` |
| `options.UserAgent = "x/1";` | `options = options with { UserAgent = "x/1" };` |
| An invalid `BaseAddress` failed at `BuildRequest` | It throws `ArgumentException` where it is set |
| Default `User-Agent` `dexpace-dotnet/<v>` | `dexpace-dotnet/<v> dotnet/<runtime>`; an unknown version reads `unknown`, not `0.0.0` |
| `Retry-After` HTTP dates in lower case, `UTC`, `+0000` or with a one-digit day were ignored | They are honoured; RFC 850 and asctime are still ignored |
