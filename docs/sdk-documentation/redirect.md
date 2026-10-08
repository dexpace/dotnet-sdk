# Redirect

**As built by phase 6b, written against source on 2026-10-08** (branch `73-phase-6b-redirect`). This page describes how
`Dexpace.Sdk.Core` follows redirects after phase 6b: the defaults, `RedirectOptions`, what is followed and how the request is
rebuilt, the predicate, the two refusals, credential hygiene, and the events. The requirement IDs it cites are the normative text
(`docs/product-spec/10-redirect-handling.md`, `REDIR-1` to `REDIR-28`); the decisions behind each type are in the
[phase 6b design](../work/mvp/phase6/phase6b/2026-10-08-phase6b-redirect-design.md) and in
[design §6.2](../sdk-design-dotnet/06-retry-redirect-and-authentication.md). Neither is restated here.

`RedirectPolicy` sits in the `Redirect` pillar of an `HttpPipeline` (installed by `DexpacePipeline.CreateDefault` and
`AddStandardResilience`), outside the retry pillar and the auth stage: each hop is a fresh drive through retry and auth, so a
credential is re-attached, or withheld, per hop. The standard async pipeline follows redirects (design §10 entry 14).

## The defaults, in one place

| | Default | To change it |
|---|---|---|
| Hops followed | `3` | `RedirectOptions.MaxRedirects`; `0` returns the first 3xx verbatim |
| Methods followed on 301, 302, 307, 308 | `GET` and `HEAD` | `RedirectOptions.AllowedMethods` |
| A `303 See Other` | returned | `RedirectOptions.FollowSeeOther = true` |
| `https` to `http` | throws `RedirectSchemeDowngradeException` | `RedirectOptions.AllowHttpsToHttpDowngrade = true` |
| A body that cannot be re-sent | throws `RedirectBodyNotReplayableException` | buffer it: `await body.ToReplayableAsync()` |
| Revisiting a URI | the 3xx is returned | not configurable (`REDIR-16`) |

All of this is `RedirectOptions`, a sealed record: derive a changed copy with `with`.

```csharp
var options = new DexpaceClientOptions
{
    Redirect = new RedirectOptions
    {
        MaxRedirects = 5,
        AllowedMethods = new HashSet<Method> { Method.Get, Method.Head, Method.Post },
        FollowSeeOther = true,
    },
};
```

## What is followed

Only `301`, `302`, `303`, `307` and `308`. Every other status, `300`, `304` and `305` included, is returned untouched, with its
`Location` ignored (`REDIR-1`, `REDIR-2`), and the predicate is not called for it.

- **`301`, `302`, `307`, `308`** are followed when the **original** request method is in `AllowedMethods`, as the **same method with
  the same body**. A `POST` is never rewritten to a `GET` (`REDIR-3`): if `POST` is not allowed, the 3xx is returned, and if it is,
  the `POST` is re-sent. "Original" is the method of the first request the policy drove (design P6b-4), so a `301` that arrives after
  an opted-in `303` is judged by the method the call began with, not by the `GET` the `303` produced.
- **`303`** is followed only with `FollowSeeOther`, whatever the method and whatever `AllowedMethods` says, and always as a body-less
  `GET`. The re-issued request has lost every header whose name starts with `Content-` (case-insensitive), because the body it
  described is gone (`REDIR-5`).
- **`AllowedMethods` is copied** when it is assigned, into a frozen set (`REDIR-26`): changing your own collection afterwards does
  nothing. An empty set is legal and means "follow only an opted-in 303"; a predicate may still follow anything.

### What stops quietly

These return the current 3xx **open** (you read and dispose it as any response), and `MaxRedirects` is never an error:

| Reason | When |
|---|---|
| not eligible | the method, the status or your predicate declines it |
| malformed `Location` | missing, empty or whitespace (`REDIR-19`, silent); unparseable, two values, a scheme other than http(s), or no host (`REDIR-18`, logged) |
| loop | the target was already visited on this call (`REDIR-16`) |
| hop cap | `MaxRedirects` hops have been followed |

A `Location` is resolved against the **current hop**, not the seed (`REDIR-14`), with userinfo dropped (`REDIR-12`) and every
reserved escape, non-default port and IPv6 literal kept (`REDIR-13`). One residue to know about: an **explicit default port is
elided**, so `Location: https://h:443/y` is sent as `https://h/y`. The origin and the meaning on the wire are the same. A `Location`
with spaces or non-ASCII characters is followed percent-encoded, because the server said so. Two `Location` values are treated as
malformed rather than guessing which one was meant.

## The predicate

```csharp
new RedirectOptions
{
    Predicate = condition => condition.Target is { Host: "api.example.com" },
};
```

`RedirectOptions.Predicate` replaces the **eligibility** decision (the allowed-method set and `FollowSeeOther`) for a recognised 3xx,
and nothing else (`REDIR-20`, design P6b-7). It is called once per 3xx in `{301, 302, 303, 307, 308}`, including when there is no
usable `Location` and at the cap (`REDIR-21`), and never for any other status. It **cannot** defeat loop detection, the hop cap, the
`Location` screen, the downgrade guard, the replay gate or credential stripping: it can make the policy stricter, never looser.

`RedirectCondition` is a snapshot: `Response` (open; read its status and headers, never dispose it or read its body),
`RedirectsFollowed` (0 on the first decision), `VisitedUris` (a copy, in order, the current request's included) and `Target`, the
policy's own resolution of `Location`, or `null` when unusable. Judge the destination with `Target` rather than parsing `Location`
yourself: a second parser that disagrees with the policy is a security bug. A predicate that throws has the response disposed and
its exception propagates unchanged.

## The two refusals

Both derive from `RedirectException : SdkException`, are thrown after the response is disposed, and are **not**
`ServiceRequestException`s: a response arrived, so nothing is "retry-safe", and no retry policy sees them. They carry no URL
property (a logger that destructures exception properties would publish a raw query token); the message names the redacted URLs.

- `RedirectSchemeDowngradeException`: an `https` hop to `http`. Set `AllowHttpsToHttpDowngrade` to follow it. A permitted downgrade
  is logged as a warning, and the hop carries no credential: it is cross-origin by scheme.
- `RedirectBodyNotReplayableException`: a method-preserving hop over a body that can be sent only once. This needs a widened
  `AllowedMethods` or a predicate, since the default `GET`/`HEAD` carry no body. Buffer the body first:

```csharp
var request = Request.Post(url, await RequestBody.FromStream(stream).ToReplayableAsync(ct));
```

A seekable `FromStream` body with a known length is already replayable. If both refusals apply, the downgrade is reported.

## Credential hygiene

These always apply and no option turns them off (`REDIR-7` to `REDIR-12`, `XCUT-17`):

- `Authorization` is removed before **every** re-issue, same-origin included. Re-attaching a credential is the auth policy's job: it
  runs on every hop and withholds its credential off the seed origin.
- `Cookie` and `Proxy-Authorization` are removed when the target's origin (scheme, host, effective port) differs from the **seed**
  request's, not from the previous hop. A same-origin hop keeps them (`REDIR-10`). What a foreign hop stripped is never restored by
  a later return to the seed origin.
- Userinfo in the target is dropped.
- Nothing is added to the request: no marker header, no `Referer`.

The end-to-end proof is `RedirectCredentialLeakTests` (redirect, retry and a real auth policy over a scripted transport) and its
wire twin `RedirectCredentialLeakWireTests`: a credential survives neither a cross-origin hop nor a retry across one.

**Residual risk.** The spec names three headers. A secret you put in a **custom** header on the seed, such as `X-Api-Key`, is not
recognised and **crosses origins** on a cross-origin hop. Send such a key with `ApiKeyAuthPolicy`, which stamps per hop and withholds
off the seed origin, instead of setting it on the request.

## Events

Five log events, written to the pipeline's logger (that of its `InstrumentationPolicy`) under the same emission guard as
`http.request`: a logger that is disabled builds nothing and one that throws never changes the call (`REDIR-28`, `OBS-20`). They carry
redacted URLs and no header or body, so `HttpLoggingOptions.Level` does not gate them.

| Event | Id | Level | When |
|---|---|---|---|
| `http.redirect.hop` | 150 | `Information` | a hop is followed (`url.full`, `dexpace.redirect.target`, `http.response.status_code`, `dexpace.redirect.hop` 1-based, `dexpace.redirect.cross_origin`) |
| `http.redirect.loop_detected` | 151 | `Warning` | a target was already visited |
| `http.redirect.scheme_downgrade_rejected` | 152 | `Warning` | an https to http hop was refused |
| `http.redirect.scheme_downgrade_permitted` | 153 | `Warning` | an https to http hop was followed under the opt-in |
| `http.redirect.location_malformed` | 154 | `Warning` | an eligible 3xx had an unusable `Location` (`dexpace.redirect.location`, redacted, not raw) |

Nothing is logged for a not-eligible 3xx, the hop cap or an absent `Location`: those are your own configuration or the server's
silence. On a recording operation span each followed hop adds one `dexpace.redirect.hop` span event
([tracing-and-metrics.md](./tracing-and-metrics.md)).

## Lifecycle

One loop, no recursion: a thousand-hop chain under `MaxRedirects = 1000` completes on both `SendAsync` and `Send` (`REDIR-23`). The
superseded response is **disposed before the next hop is driven** (`REDIR-22`(a)), so a one-connection pool never deadlocks; every
stop above returns the in-flight response open (`REDIR-22`(c)); the two refusals and a throwing predicate dispose it first
(`REDIR-22`(b)), a failing dispose riding the exception's suppressed trail. The call's token is checked between hops, after the
dispose and before the next drive, so a cancelled call never starts another hop.

## Getting the 3xx yourself

Set `MaxRedirects = 0`. The first 3xx is returned verbatim, open, and nothing is followed (`REDIR-25`; the async pipeline follows
by default, design §10 entry 14).

## Not built

`REDIR-27` (a configurable `Location` header name, a MAY) is declined for v1: the header is `Location`. See
[`first-release.md`](../first-release.md).
