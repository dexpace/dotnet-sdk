// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/xcut/security-by-default.conformance.test.ts and ruby-sdk@5b17395
// gems/dexpace-conformance/lib/dexpace/conformance/invariant_suite/ (security by default). The dispatch-time half of XCUT-18
// (a transport re-checks what reaches the wire) stays a transport assertion (P10-6).
using System.Text;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>XCUT-16</c> to <c>XCUT-19</c>: no credential over a non-https scheme, credential hygiene on every redirect hop (the redirect
/// loop on its own, and with the subject's auth step under it), header names and values validated before a transport sees them,
/// and secrets redacted by default in every rendering.
/// </summary>
internal static class SecurityInvariants
{
    private static readonly string[] s_credentialHeaders = ["Authorization", "Cookie", "Proxy-Authorization"];

    internal static ConformanceAssertion[] Assertions() =>
    [
        InvariantAssertionBuilder.Define("xcut-16.no-credential-over-http", ["XCUT-16"], RequirementLevel.Must, InvariantAssertionBuilder.Both, NoCredentialOverHttpAsync),
        InvariantAssertionBuilder.Define("xcut-17.redirect-hygiene", ["XCUT-17"], RequirementLevel.Must, InvariantAssertionBuilder.Both, RedirectHygieneAsync),
        InvariantAssertionBuilder.Define("xcut-18.model-validation", ["XCUT-18"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, ModelValidationAsync),
        InvariantAssertionBuilder.Define("xcut-19.redaction-default-deny", ["XCUT-19"], RequirementLevel.Must, InvariantAssertionBuilder.Both, RedactionDefaultDenyAsync),
    ];

    // A credential-stamping step refuses http before any fetch or header write; a credential-free re-issue still proceeds.
    private static async Task NoCredentialOverHttpAsync(InvariantContext context, CancellationToken token)
    {
        var plain = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0) };

        var basicOverHttp = context.Transport(ProbeTransport.Behavior.Status(200));
        var failure = await context.FailureOfAsync(WithAuth(context, basicOverHttp, plain, context.Subject.Auth.Basic("svc-user", "svc-secret")), Request.Get("http://example.test/x"), "a Basic-authenticated call over http", token).ConfigureAwait(false);
        Check.True(failure is not null, "a credential must not be stamped over http: the call must fail before it is sent", "a failure", "the call succeeded");
        Check.Equal(basicOverHttp.Sends, 0, "the requests that reached the transport from a Basic-authenticated call over http (the guard comes before the header write)");

        var fetching = new GatedCredential(context.Time, gated: false);
        var bearerOverHttp = context.Transport(ProbeTransport.Behavior.Status(200));
        failure = await context.FailureOfAsync(WithAuth(context, bearerOverHttp, plain, context.Subject.Auth.Bearer(fetching)), Request.Get("http://example.test/x"), "a bearer-authenticated call over http", token).ConfigureAwait(false);
        Check.True(failure is not null, "a credential must not be stamped over http: the call must fail before it is sent", "a failure", "the call succeeded");
        Check.Equal(fetching.Fetches, 0, "the token fetches of a bearer-authenticated call over http (the guard comes before the fetch)");
        Check.Equal(bearerOverHttp.Sends, 0, "the requests that reached the transport from a bearer-authenticated call over http");

        var secure = context.Transport(ProbeTransport.Behavior.Status(200));
        failure = await context.FailureOfAsync(WithAuth(context, secure, plain, context.Subject.Auth.Basic("svc-user", "svc-secret")), Request.Get("https://example.test/x"), "a Basic-authenticated call over https", token).ConfigureAwait(false);
        Check.True(failure is null, "the guard must only refuse a non-secure scheme: a Basic call over https must succeed", "success", Outcome.Name(failure));
        Check.True(secure.Requests.Count == 1 && secure.Requests[0].Headers.Contains("Authorization"), "a Basic call over https must carry its credential", "one request with Authorization", secure.Requests.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " requests");

        // A permitted https to http hop: the re-issue is credential-free, so the guard does not refuse it.
        var hops = context.Transport(Script(("https://example.test/start", 302, "http://example.test/next")));
        var downgrade = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0), Redirect = new RedirectOptions { AllowHttpsToHttpDowngrade = true } };
        failure = await context.FailureOfAsync(WithAuth(context, hops, downgrade, context.Subject.Auth.Basic("svc-user", "svc-secret")), Request.Get("https://example.test/start"), "a call redirected from https to http", token).ConfigureAwait(false);
        Check.True(failure is null, "a credential-free re-issue over http must proceed", "success", Outcome.Name(failure));
        Check.True(hops.Requests.Count == 2 && !hops.Requests[1].Headers.Contains("Authorization"), "the re-issue over http must carry no credential", "the second request without Authorization", Describe(hops.Requests));
    }

    // Authorization is stripped on every hop; Cookie and Proxy-Authorization on a cross-origin hop, judged against the seed.
    private static async Task RedirectHygieneAsync(InvariantContext context, CancellationToken token)
    {
        var seed = Request.Get("https://a.example.test/start")
            .WithHeader("Authorization", "Bearer " + SecurityVectors.AuthorizationSecret)
            .WithHeader("Cookie", "sid=" + SecurityVectors.CookieSecret)
            .WithHeader("Proxy-Authorization", "Basic " + SecurityVectors.AuthorizationSecret);
        var options = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0) };

        var sameOrigin = context.Transport(Script(("https://a.example.test/start", 302, "https://a.example.test/next")));
        await Followed(context, sameOrigin, options, seed, "a same-origin redirect", token).ConfigureAwait(false);
        var hop = sameOrigin.Requests[1];
        Check.True(!hop.Headers.Contains("Authorization"), "Authorization must be stripped before every redirect re-issue, even a same-origin one", "no Authorization on the re-issue", "Authorization present");
        Check.True(hop.Headers.Contains("Cookie") && hop.Headers.Contains("Proxy-Authorization"), "Cookie and Proxy-Authorization are kept on a same-origin hop", "both kept", Describe(sameOrigin.Requests));

        var crossOrigin = context.Transport(Script(("https://a.example.test/start", 302, "https://b.example.test/x")));
        await Followed(context, crossOrigin, options, seed, "a cross-origin redirect", token).ConfigureAwait(false);
        AssertNoCredentials(crossOrigin.Requests[1], "a cross-origin redirect");

        var twice = context.Transport(Script(
            ("https://a.example.test/start", 302, "https://b.example.test/x"),
            ("https://b.example.test/x", 302, "https://b.example.test/y")));
        await Followed(context, twice, options, seed, "two hops to a foreign origin", token).ConfigureAwait(false);
        AssertNoCredentials(twice.Requests[2], "the second hop to the foreign origin (origin is judged against the seed, not the previous hop)");

        var withUserInfo = context.Transport(Script(("https://a.example.test/start", 302, "https://" + SecurityVectors.UserInfo + "@a.example.test/next")));
        await Followed(context, withUserInfo, options, seed, "a redirect whose Location carries userinfo", token).ConfigureAwait(false);
        Check.True(withUserInfo.Requests[1].Url.UserInfo.Length == 0, "userinfo in a Location must be dropped before the re-issue", "no userinfo", "userinfo present");

        var downgrade = context.Transport(Script(("https://a.example.test/start", 302, "http://a.example.test/next")));
        var refused = await context.FailureOfAsync(context.Build(downgrade, options), seed, "a redirect from https to http", token).ConfigureAwait(false);
        Check.True(refused is not null, "an https to http redirect must be refused unless the downgrade is enabled", "a failure", "the call succeeded");
        Check.Equal(downgrade.Sends, 1, "the requests sent for a refused downgrade (the http hop is never sent)");

        // XCUT-17(b), the half the hand-seeded headers above cannot show: the subject's own credential-stamping step under the redirect loop.
        await AuthUnderRedirectAsync(context, "a Basic step", "Basic ", () => context.Subject.Auth.Basic("svc-user", "svc-secret"), token).ConfigureAwait(false);
        await AuthUnderRedirectAsync(context, "a bearer step", "Bearer ", () => context.Subject.Auth.Bearer(new GatedCredential(context.Time, gated: false)), token).ConfigureAwait(false);
    }

    // The auth step sits inside the redirect loop, so it stamps every hop it judges to be the seed's origin and none it does not: the
    // same-origin re-issue is stamped again, a foreign hop (and a retry of one) carries no credential, and coming back to the seed's
    // origin stamps again. The first request of each call must carry the stamp, or "no credential on the hop" would prove nothing.
    private static async Task AuthUnderRedirectAsync(InvariantContext context, string step, string scheme, Func<HttpPipelinePolicy> auth, CancellationToken token)
    {
        var seed = Request.Get("https://a.example.test/start")
            .WithHeader("Cookie", "sid=" + SecurityVectors.CookieSecret)
            .WithHeader("Proxy-Authorization", "Basic " + SecurityVectors.AuthorizationSecret);
        var plain = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0) };

        var sameOrigin = context.Transport(Script(("https://a.example.test/start", 302, "https://a.example.test/next")));
        await Followed(context, WithAuth(context, sameOrigin, plain, auth()), sameOrigin, seed, $"a same-origin redirect under {step}", token).ConfigureAwait(false);
        AssertStamped(sameOrigin.Requests[0], scheme, $"the seed request under {step}");
        AssertStamped(sameOrigin.Requests[1], scheme, $"the same-origin re-issue under {step} (Authorization is stripped before the hop, and the credential is re-stamped by the auth step)");

        var crossOrigin = context.Transport(Script(("https://a.example.test/start", 302, "https://b.example.test/x")));
        await Followed(context, WithAuth(context, crossOrigin, plain, auth()), crossOrigin, seed, $"a cross-origin redirect under {step}", token).ConfigureAwait(false);
        AssertStamped(crossOrigin.Requests[0], scheme, $"the seed request under {step}");
        AssertNoCredentials(crossOrigin.Requests[1], $"a cross-origin redirect under {step} (no credential re-stamped on the foreign host)");

        var there = context.Transport(Script(
            ("https://a.example.test/start", 302, "https://b.example.test/x"),
            ("https://b.example.test/x", 302, "https://a.example.test/back")));
        await Followed(context, WithAuth(context, there, plain, auth()), there, seed, $"a hop to a foreign origin and back under {step}", token).ConfigureAwait(false);
        AssertNoCredentials(there.Requests[1], $"the foreign hop under {step}");
        AssertStamped(there.Requests[2], scheme, $"the hop back to the seed's origin under {step} (origin is judged against the seed, so the credential is re-stamped)");
        Check.True(!there.Requests[2].Headers.Contains("Cookie") && !there.Requests[2].Headers.Contains("Proxy-Authorization"), $"a stripped Cookie or Proxy-Authorization must not be restored on the hop back under {step}", "neither", Describe(there.Requests));

        // A permitted https to http hop is foreign to the seed, so it is credential-free and the https guard does not refuse it; its retry
        // re-sends the stripped request and must not attach the credential again.
        var retried = context.Transport(Replies(
            request => ProbeTransport.Empty(request, 302, new Headers.Builder().Add("Location", "http://a.example.test/next").Build()),
            request => ProbeTransport.Empty(request, 503),
            request => ProbeTransport.Empty(request, 200)));
        var permitted = new DexpaceClientOptions { Retry = InvariantOptions.Fast(1), Redirect = new RedirectOptions { AllowHttpsToHttpDowngrade = true } };
        await Followed(context, WithAuth(context, retried, permitted, auth()), retried, seed, $"a permitted downgrade followed by a retry under {step}", token).ConfigureAwait(false);
        Check.Equal(retried.Requests.Count, 3, $"the requests sent for a permitted downgrade whose first answer is a 503 under {step} (seed, the http hop, its retry)");
        AssertStamped(retried.Requests[0], scheme, $"the seed request under {step}");
        AssertNoCredentials(retried.Requests[1], $"the permitted https to http hop under {step}");
        AssertNoCredentials(retried.Requests[2], $"the retry of the permitted https to http hop under {step} (a retried foreign hop is re-sent with no credential)");
    }

    private static void AssertStamped(Request request, string scheme, string where)
    {
        var values = request.Headers.GetAll("Authorization");
        Check.True(
            values.Count == 1 && values[0].StartsWith(scheme, StringComparison.OrdinalIgnoreCase),
            $"{where} must carry exactly the credential its auth step stamps",
            $"one Authorization starting with \"{scheme}\"",
            values.Count == 0 ? "no Authorization" : string.Join(" | ", values.Select(value => value.Length > scheme.Length ? value[..scheme.Length] + "..." : value)));
    }

    private static Task Followed(InvariantContext context, ProbeTransport transport, DexpaceClientOptions options, Request seed, string what, CancellationToken token) =>
        Followed(context, context.Build(transport, options), transport, seed, what, token);

    private static async Task Followed(InvariantContext context, HttpPipeline pipeline, ProbeTransport transport, Request seed, string what, CancellationToken token)
    {
        var failure = await context.FailureOfAsync(pipeline, seed, what, token).ConfigureAwait(false);
        Check.True(failure is null, $"{what} must be followed", "success", Outcome.Name(failure));
        Check.True(transport.Requests.Count >= 2, $"{what} must be re-issued", "at least 2 requests", transport.Requests.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void AssertNoCredentials(Request hop, string where) =>
        Check.True(
            !hop.Headers.Contains("Authorization") && !hop.Headers.Contains("Cookie") && !hop.Headers.Contains("Proxy-Authorization"),
            $"on {where}, Authorization, Cookie and Proxy-Authorization must all be stripped",
            "none of the three",
            string.Join(", ", s_credentialHeaders.Where(hop.Headers.Contains)));

    // Names reject every C0 control, DEL and non-ASCII; outbound values the same except HTAB; inbound values permit obs-text.
    private static Task ModelValidationAsync(InvariantContext context, CancellationToken token)
    {
        var models = context.Subject.Models;

        foreach (var c in SecurityVectors.NameRejects)
        {
            RejectsArgument(() => models.OutboundHeader("X-" + c + "-Y", "v"), $"an outbound header name containing {SecurityVectors.Describe(c)} inside it");
            RejectsArgument(() => models.InboundHeader("X-" + c + "-Y", "v"), $"an inbound header name containing {SecurityVectors.Describe(c)} inside it");

            // HTAB at the end of a name is where two MUSTs meet: XCUT-18 refuses a name with HTAB, and HTTP-17 trims an outbound
            // (caller-set) name's surrounding whitespace before validating it. A model may do either, so the check is the safety property both
            // share: the name is refused, or it is stored as exactly the trimmed token and never with the tab. The same is accepted
            // of an inbound name, which HTTP-19 says stays strictly validated: pinning the trim there would assert what no row supports, and
            // a model that refuses it follows the text, so the inbound trim is reported as a split clause (checklist, XCUT-18) and not as a pass condition.
            // Every other character of the set is refused at the ends too.
            foreach (var edged in new[] { c + "X", "X" + c })
            {
                if (c == '\t')
                {
                    RefusesOrStoresTrimmed(() => models.OutboundHeader(edged, "v"), "an outbound header name with HTAB at one end (refused, or trimmed under HTTP-17)");
                    RefusesOrStoresTrimmed(() => models.InboundHeader(edged, "v"), "an inbound header name with HTAB at one end (refused, or trimmed as the lenient path does)");
                }
                else
                {
                    RejectsArgument(() => models.OutboundHeader(edged, "v"), $"an outbound header name with {SecurityVectors.Describe(c)} at one end");
                    RejectsArgument(() => models.InboundHeader(edged, "v"), $"an inbound header name with {SecurityVectors.Describe(c)} at one end");
                }
            }
        }

        foreach (var c in SecurityVectors.OutboundValueRejects)
        {
            RejectsArgument(() => models.OutboundHeader("X-Probe", "a" + c + "b"), $"an outbound header value containing {SecurityVectors.Describe(c)}");
        }

        foreach (var c in SecurityVectors.InboundValueRejects)
        {
            RejectsArgument(() => models.InboundHeader("X-Probe", "a" + c + "b"), $"an inbound header value containing {SecurityVectors.Describe(c)}");
        }

        Check.Equal(models.OutboundHeader("X-Probe", "a\tb").Get("X-Probe"), "a\tb", "an outbound header value with HTAB inside it (accepted)");
        Check.Equal(models.InboundHeader("X-Probe", "a\tb").Get("X-Probe"), "a\tb", "an inbound header value with HTAB inside it (accepted)");
        Check.Equal(models.InboundHeader("X-Probe", "café").Get("X-Probe"), "café", "an inbound header value with obs-text (accepted)");
        return Task.CompletedTask;
    }

    // Refused with an argument error, or built with the one name "X": the trimmed token, never the name with its tab.
    private static void RefusesOrStoresTrimmed(Func<Headers> build, string what)
    {
        Headers? built = null;
        Exception? thrown = null;
        try
        {
            built = build();
        }
#pragma warning disable CA1031 // The point of the method: what the model does with a hostile character is the answer.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            thrown = ex;
        }

        if (thrown is not null)
        {
            Check.True(thrown is ArgumentException, $"{what} must be refused with an argument error or stored trimmed", "ArgumentException or the stored name X", Outcome.Name(thrown));
            return;
        }

        Check.Equal(string.Join(",", built!.Names), "X", $"the stored name of {what}");
    }

    private static void RejectsArgument(Func<Headers> build, string what)
    {
        Exception? thrown = null;
        try
        {
            _ = build();
        }
#pragma warning disable CA1031 // The point of the method: what the model does with a hostile character is the answer.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            thrown = ex;
        }

        Check.True(thrown is ArgumentException, $"{what} must be rejected with an argument error", "ArgumentException", Outcome.Name(thrown));
    }

    // Secrets are redacted by default in URLs, headers, credential renderings and logs, and no body is logged unless asked.
    private static async Task RedactionDefaultDenyAsync(InvariantContext context, CancellationToken token)
    {
        var subject = context.Subject;
        var redacted = subject.RedactUrl(SecurityVectors.SecretUrl);
        AssertNoSecrets(redacted, "a redacted URL (userinfo, query values and fragment tokens)");
        AssertNoSecrets(subject.RedactHeaderValue(SecurityVectors.SecretLocation), "a redacted URL-valued header value");

        foreach (var credential in subject.Credentials)
        {
            Check.True(
                !credential.Render().Contains(credential.Secret, StringComparison.Ordinal),
                $"a {credential.Kind} must not reveal its secret in its string form",
                "the secret absent",
                "the secret present");
        }

        // The pipeline logs headers by default-deny: only the allow-list is verbatim; nothing is logged with a body.
        var transport = context.Transport(ProbeTransport.Behavior.Of((request, _) => new Response(
            request,
            Status.Ok,
            Protocol.Http11,
            new Headers.Builder().Add("Content-Type", "text/plain").Add("Set-Cookie", "s=" + SecurityVectors.SetCookieSecret).Add("X-Custom", SecurityVectors.CustomHeaderSecret).Build(),
            ResponseBody.FromBytes(Encoding.UTF8.GetBytes("body=" + SecurityVectors.BodySecret)),
            null)));
        var seed = Request.Get(InvariantOptions.Url + "?token=" + SecurityVectors.QuerySecret + "&api-version=1")
            .WithHeader("Authorization", "Bearer " + SecurityVectors.AuthorizationSecret)
            .WithHeader("Cookie", "sid=" + SecurityVectors.CookieSecret)
            .WithHeader("Accept", "application/json");
        foreach (var level in new[] { HttpLogLevel.None, HttpLogLevel.Headers })
        {
            var options = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0), Logging = new HttpLoggingOptions { Level = level } };
            var failure = await context.FailureOfAsync(context.Build(transport, options), seed, $"a call logged at the {level} level", token).ConfigureAwait(false);
            Check.True(failure is null, "the logged call must succeed", "success", Outcome.Name(failure));
        }

        var rendered = context.Logger.Entries;
        foreach (var secret in new[] { SecurityVectors.QuerySecret, SecurityVectors.AuthorizationSecret, SecurityVectors.CookieSecret, SecurityVectors.SetCookieSecret, SecurityVectors.CustomHeaderSecret, SecurityVectors.BodySecret })
        {
            Check.True(
                !context.Logger.Leaks(secret),
                "logging must redact secrets by default: header values outside the allow-list, query values, and every body",
                "no log line carrying " + secret,
                rendered.Count + " log lines, one carrying it");
        }

        if (rendered.Count == 0)
        {
            throw new ConformanceVacuousException("the pipeline logged nothing at the Headers level, so there was nothing to redact");
        }
    }

    private static void AssertNoSecrets(string text, string what)
    {
        var leaked = SecurityVectors.UrlSecrets.Where(secret => text.Contains(secret, StringComparison.Ordinal)).ToArray();
        Check.True(leaked.Length == 0, $"{what} must not reveal a secret", "none of the secrets", string.Join(", ", leaked));
    }

    private static HttpPipeline WithAuth(InvariantContext context, ProbeTransport transport, DexpaceClientOptions options, HttpPipelinePolicy auth) =>
        PipelineBuilder.Flatten(context.Build(transport, options)).Add(auth).Build();

    private static ProbeTransport.Behavior Script(params (string From, int Status, string? Location)[] hops) =>
        ProbeTransport.Behavior.Of((request, _) =>
        {
            var key = request.Url.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.Unescaped);
            var hop = hops.FirstOrDefault(entry => entry.From == key);
            return hop.From is null
                ? ProbeTransport.Empty(request, 200)
                : ProbeTransport.Empty(request, hop.Status, new Headers.Builder().Add("Location", hop.Location!).Build());
        });

    // The n-th send is answered by the n-th reply; the last one answers every send after it.
    private static ProbeTransport.Behavior Replies(params Func<Request, Response>[] replies) =>
        ProbeTransport.Behavior.Of((request, send) => replies[Math.Min(send, replies.Length) - 1](request));

    private static string Describe(IReadOnlyList<Request> requests) =>
        string.Join("; ", requests.Select(request => $"{request.Url.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.Unescaped)} [{string.Join(", ", request.Headers.Names)}]"));
}
