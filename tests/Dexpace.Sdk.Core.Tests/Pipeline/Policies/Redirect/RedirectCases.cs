// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The REDIR-1 to REDIR-21 and REDIR-26 decision matrix, ported from nodejs-sdk@54aeed4
// packages/core/src/redirect/decide.test.ts in its section order (line ranges cited per group), with the changes design
// 6b lists: the four marker rows (L669, L684, L902, L922) become the one P6b-23 row; L376 (a configurable Location
// header, REDIR-27) is dropped; the "shared return-current value" section (L118-128, JavaScript identity) is dropped;
// the fast-check properties are fixed examples; the "CURRENT hop" row (L758-802) is inverted to the literal reading of
// REDIR-3 (P6b-4). Added rows with no Node source are marked "added". Ruby step_test.rb DecisionTest (L140), PredicateTest
// (L202), CredentialHygieneTest (L319), LocationTest (L466), RefusedTargetTest (L542) and ReissueTest (L608) are covered
// by the same rows where Node's did not already hold them. Not ported: MarkerTest (L407, a cursor marker this port
// retires), ForkingTest (L956, a fork primitive this port lacks) and matrix_facts_test.rb (Ruby host facts).

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Pipeline.Policies;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>The rows of the decision matrix, keyed by name (R9).</summary>
internal static class RedirectCases
{
    private const string Host = "https://api.example.com";
    private static readonly string[] s_both = ["Cookie", "Proxy-Authorization"];
    private static readonly string[] s_all3 = ["Authorization", "Cookie", "Proxy-Authorization"];
    private static readonly (string, string)[] s_creds =
    [
        ("Authorization", "Bearer x"), ("Cookie", "a=b"), ("Proxy-Authorization", "Basic y"), ("X-Keep", "k"),
    ];

    private static readonly List<RedirectCase> s_all = Build();

    internal static IReadOnlyList<string> Names() => [.. s_all.Select(static c => c.Name)];

    internal static RedirectCase Named(string name) => s_all.Single(c => c.Name == name);

    private static RedirectOptions Opt(
        int max = 3,
        bool downgrade = false,
        bool see = false,
        Method[]? methods = null,
        Func<RedirectCondition, bool>? predicate = null) =>
        methods is null
            ? new RedirectOptions { MaxRedirects = max, AllowHttpsToHttpDowngrade = downgrade, FollowSeeOther = see, Predicate = predicate }
            : new RedirectOptions
            {
                MaxRedirects = max,
                AllowHttpsToHttpDowngrade = downgrade,
                FollowSeeOther = see,
                Predicate = predicate,
                AllowedMethods = methods.ToHashSet(),
            };

    private static Expect Stop(RedirectStopReason reason, bool? malformed = null, int? calls = null) =>
        new(RedirectDecisionKind.ReturnCurrent) { Reason = reason, Malformed = malformed, PredicateCalls = calls };

    private static Expect Go(string url, string method = "GET", int? calls = null) =>
        new(RedirectDecisionKind.Follow) { NextMethod = method, NextUrl = url, PredicateCalls = calls };

    private static Expect Boom<T>() => new(RedirectDecisionKind.Fail) { ExceptionType = typeof(T) };

    private static RedirectCase Row(string name, int status, Expect expect, string location = "/next") =>
        new(name, status, expect) { LocationValues = location.Length == 0 ? [] : [location] };

    private static List<RedirectCase> Build()
    {
        List<RedirectCase> rows = [];
        rows.AddRange(FastPath());
        rows.AddRange(Eligibility());
        rows.AddRange(Predicates());
        rows.AddRange(LocationRows());
        rows.AddRange(LoopAndCap());
        rows.AddRange(DowngradeAndReplay());
        rows.AddRange(Headers());
        rows.AddRange(SeeOther());
        return rows;
    }

    // fast path (decide.test.ts L129-162): REDIR-1, REDIR-2, REDIR-21.
    private static IEnumerable<RedirectCase> FastPath()
    {
        foreach (var status in new[] { 100, 200, 204, 300, 304, 305, 306, 399 })
        {
            yield return Row($"REDIR-1: a {status} with a Location is not a redirect", status, Stop(RedirectStopReason.NotARedirect, calls: 0))
                with
            { Options = Opt(predicate: static _ => true) };
        }

        yield return Row("REDIR-2: a 300 Multiple Choices is returned, not followed", 300, Stop(RedirectStopReason.NotARedirect));
        yield return Row("REDIR-2: a 304 is returned, not followed", 304, Stop(RedirectStopReason.NotARedirect));
        yield return Row("REDIR-2: a 305 is returned, not followed", 305, Stop(RedirectStopReason.NotARedirect));
    }

    // eligibility, 301/302/307/308 and the allowed-method set: REDIR-3, REDIR-4, REDIR-26 (L705-757, L758-802).
    private static IEnumerable<RedirectCase> Eligibility()
    {
        foreach (var status in new[] { 301, 302, 307, 308 })
        {
            yield return Row($"REDIR-3: a {status} on a GET follows as a GET", status, Go($"{Host}/next"));
            yield return Row($"REDIR-4: a {status} on a HEAD follows as a HEAD", status, Go($"{Host}/next", "HEAD")) with { Method = "HEAD" };
            yield return Row($"REDIR-3: a {status} on a POST is not eligible under the default set", status, Stop(RedirectStopReason.NotEligible))
                with
            { Method = "POST", Body = BodyKind.Bytes };
            yield return Row($"REDIR-3: a {status} on a body-less POST is not eligible under the default set", status, Stop(RedirectStopReason.NotEligible))
                with
            { Method = "POST" };
        }

        foreach (var method in new[] { "PUT", "DELETE", "PATCH", "OPTIONS", "TRACE", "CONNECT" })
        {
            yield return Row($"REDIR-3: a 301 on {method} is not eligible under the default set (added)", 301, Stop(RedirectStopReason.NotEligible))
                with
            { Method = method };
        }

        foreach (var status in new[] { 301, 302, 307, 308 })
        {
            yield return Row($"REDIR-3: a {status} on a POST in AllowedMethods keeps POST and the body", status, new Expect(RedirectDecisionKind.Follow) { NextMethod = "POST", NextUrl = $"{Host}/next", SameBody = true })
                with
            { Method = "POST", Body = BodyKind.Bytes, Options = Opt(methods: [Method.Post]) };
        }

        yield return Row("REDIR-3: AllowedMethods containing POST follows a body-less POST (BODY-5, added)", 307, Go($"{Host}/next", "POST"))
            with
        { Method = "POST", Options = Opt(methods: [Method.Post]) };
        yield return Row("REDIR-3: an empty AllowedMethods follows no 301 (added)", 301, Stop(RedirectStopReason.NotEligible))
            with
        { Options = Opt(methods: []) };
        yield return Row("REDIR-3: an empty AllowedMethods follows an opted-in 303 (added)", 303, Go($"{Host}/next"))
            with
        { Options = Opt(see: true, methods: []) };
        yield return Row("REDIR-3: eligibility is judged on the ORIGINAL method, not the current hop (inverted Node L758-802, P6b-4)", 301, Stop(RedirectStopReason.NotEligible))
            with
        { Method = "POST", Visited = [$"{Host}/x"] };
        yield return Row("REDIR-3: eligibility on the original method, allowed original follows later hops (added)", 301, Go($"{Host}/next", "GET"))
            with
        { Method = "GET", Visited = [$"{Host}/x"] };
    }

    // predicate override and safety mechanics (L163-242): REDIR-20, REDIR-21, P6b-7.
    private static IEnumerable<RedirectCase> Predicates()
    {
        yield return Row("REDIR-20: a predicate answering true follows a method outside the allowed set", 302, Go($"{Host}/next", "POST", calls: 1))
            with
        { Method = "POST", Options = Opt(predicate: static _ => true) };
        yield return Row("REDIR-20: a predicate answering false stops a GET", 302, Stop(RedirectStopReason.NotEligible, calls: 1))
            with
        { Options = Opt(predicate: static _ => false) };
        yield return Row("REDIR-20: a predicate sees the snapshot", 302, Go($"{Host}/next", calls: 1))
            with
        {
            Followed = 1,
            Options = Opt(predicate: static c =>
            {
                Xunit.Assert.Equal(302, c.Response.Status.Code);
                Xunit.Assert.Equal(1, c.RedirectsFollowed);
                Xunit.Assert.Equal($"{Host}/next", c.Target!.AbsoluteUri);
                Xunit.Assert.Equal(2, c.VisitedUris.Count);
                return true;
            }),
        };
        yield return Row("REDIR-21: a predicate is called with no Location", 302, Stop(RedirectStopReason.MalformedLocation, malformed: false, calls: 1), location: string.Empty)
            with
        {
            Options = Opt(predicate: static c =>
            {
                Xunit.Assert.Null(c.Target);
                return true;
            }),
        };
        yield return Row("REDIR-21: a predicate is called with an unusable Location", 302, Stop(RedirectStopReason.MalformedLocation, malformed: true, calls: 1), location: "ftp://x/y")
            with
        { Options = Opt(predicate: static _ => true) };
        yield return Row("REDIR-21: a predicate is called at the cap", 302, Stop(RedirectStopReason.HopCap, calls: 1))
            with
        { Followed = 3, Options = Opt(predicate: static _ => true) };
        yield return Row("REDIR-20: a predicate cannot defeat loop detection", 302, Stop(RedirectStopReason.LoopDetected, calls: 1), location: $"{Host}/a/b")
            with
        { Options = Opt(predicate: static _ => true) };
        yield return Row("REDIR-20: a predicate cannot defeat the downgrade guard", 302, Boom<RedirectSchemeDowngradeException>(), location: "http://api.example.com/x")
            with
        { Options = Opt(predicate: static _ => true) };
        yield return Row("REDIR-20: a predicate cannot defeat the replay gate", 307, Boom<RedirectBodyNotReplayableException>())
            with
        { Method = "POST", Body = BodyKind.SingleUse, Options = Opt(predicate: static _ => true) };
        yield return Row("REDIR-20: a predicate cannot force a malformed Location", 302, Stop(RedirectStopReason.MalformedLocation, malformed: true), location: "mailto:a@b")
            with
        { Options = Opt(predicate: static _ => true) };
    }

    // Location resolution (L243-308), unfollowed paths (L309-358), totality (L359-406), RFC 3986 forms (L837-884): REDIR-12..14, 18, 19.
    private static IEnumerable<RedirectCase> LocationRows()
    {
        yield return Row("REDIR-14: a relative Location resolves against the current hop", 302, Go($"{Host}/a/c"), "c");
        yield return Row("REDIR-14: an absolute-path Location resolves", 302, Go($"{Host}/x?y=1"), "/x?y=1");
        yield return Row("REDIR-14: dot segments are removed", 302, Go($"{Host}/z"), "../z");
        yield return Row("REDIR-14: a query-only Location resolves", 302, Go($"{Host}/a/b?q=2"), "?q=2");
        yield return Row("REDIR-14: a fragment-only Location resolves (added, Ruby round 2)", 302, Go($"{Host}/a/b#frag"), "#frag");
        yield return Row("REDIR-14: a fragment-bearing Location resolves (added)", 302, Go($"{Host}/c#frag"), "/c#frag");
        yield return Row("REDIR-14: a protocol-relative Location keeps the scheme", 302, Go("https://other.example/p"), "//other.example/p");
        yield return Row("REDIR-14: an absolute Location is used as is", 302, Go("https://other.example/p?q=1"), "https://other.example/p?q=1");
        yield return Row("REDIR-14: resolution is against the current hop, not the seed", 302, Go($"{Host}/m/c"))
            with
        { SeedUrl = "https://seed.example/start", LocationValues = ["c"], Url = $"{Host}/m/n", Visited = [] };
        yield return Row("REDIR-12: userinfo in the Location is dropped", 302, Go("https://other.example/y"), "https://u:p@other.example/y");
        yield return Row("REDIR-13: reserved escapes survive", 302, Go($"{Host}/y%2Fz?a=%26"), "https://u:p@api.example.com/y%2Fz?a=%26");
        yield return Row("REDIR-13: an IPv6 literal and a non-default port survive", 302, Go("https://[::1]:8443/y"), "https://u:p@[::1]:8443/y");
        yield return Row("REDIR-13: an explicit default port is elided (P6b-30, residue)", 302, Go($"{Host}/y"), "https://api.example.com:443/y");
        yield return Row("REDIR-18: a Location with a space is followed percent-encoded", 302, Go($"{Host}/a%20b"), "/a b");
        yield return Row("REDIR-19: a missing Location is returned unfollowed", 302, Stop(RedirectStopReason.MalformedLocation, malformed: false), string.Empty);
        yield return Row("REDIR-19: a whitespace Location is returned unfollowed", 302, Stop(RedirectStopReason.MalformedLocation, malformed: false), "   ");
        foreach (var bad in new[] { "ftp://x/y", "mailto:a@b", "javascript:1", "file:///etc/passwd", "http:///p", "https://", "http:foo", "FTP://x/y", "http://a:99999/", "http://[::1", "http://\u00e4.xn--zz/", "http://\uffff/", "http://a\u200db/" })
        {
            yield return Row($"REDIR-18: the Location '{bad}' is malformed", 302, Stop(RedirectStopReason.MalformedLocation, malformed: true), bad);
        }

        yield return new RedirectCase("REDIR-18: two Location values are malformed (added, P6b-10)", 302, Stop(RedirectStopReason.MalformedLocation, malformed: true))
        { LocationValues = ["/one", "/two"] };
    }

    // loop detection (L407-438, L803-836) and hop cap (L439-490): REDIR-16, REDIR-17.
    private static IEnumerable<RedirectCase> LoopAndCap()
    {
        yield return Row("REDIR-16: A to B to A is a loop", 302, Stop(RedirectStopReason.LoopDetected), $"{Host}/a/b")
            with
        { Visited = [$"{Host}/b"] };
        yield return Row("REDIR-16: a revisit of a userinfo-bearing seed is a loop", 302, Stop(RedirectStopReason.LoopDetected), $"{Host}/a/b")
            with
        { Url = "https://u:p@api.example.com/a/b", Visited = [$"{Host}/b"] };
        yield return Row("REDIR-16: a Location pointing at the current URL is a loop", 302, Stop(RedirectStopReason.LoopDetected), $"{Host}/a/b");
        yield return Row("REDIR-16: loop detection survives case and default-port re-spelling", 302, Stop(RedirectStopReason.LoopDetected), "HTTPS://API.EXAMPLE.COM:443/a/b");
        yield return Row("REDIR-16: a different fragment is a different key", 302, Go($"{Host}/a/b#other"), "#other");
        yield return Row("REDIR-16: a different query is a different key", 302, Go($"{Host}/a/b?p=2"), "?p=2");

        foreach (var (max, followed, follows) in new[] { (0, 0, false), (1, 0, true), (1, 1, false), (3, 2, true), (3, 3, false), (3, 5, false), (0, 3, false) })
        {
            yield return Row(
                $"REDIR-17: MaxRedirects {max} with {followed} followed {(follows ? "follows" : "stops at the cap")}",
                302,
                follows ? Go($"{Host}/next") : Stop(RedirectStopReason.HopCap))
                with
            { Followed = followed, Options = Opt(max: max) };
        }
    }

    // scheme-downgrade guard (L491-554) and body replayability gate (L555-611): REDIR-6, REDIR-15.
    private static IEnumerable<RedirectCase> DowngradeAndReplay()
    {
        yield return Row("REDIR-15: https to http without the opt-in fails", 302, Boom<RedirectSchemeDowngradeException>(), "http://api.example.com/x");
        yield return Row("REDIR-15: https to http with the opt-in follows, flagged", 302, new Expect(RedirectDecisionKind.Follow) { NextMethod = "GET", NextUrl = "http://api.example.com/x", Downgraded = true, CrossOrigin = true }, "http://api.example.com/x")
            with
        { Options = Opt(downgrade: true) };
        yield return Row("REDIR-15: http to https follows, not flagged", 302, new Expect(RedirectDecisionKind.Follow) { NextMethod = "GET", NextUrl = "https://api.example.com/x", Downgraded = false }, "https://api.example.com/x")
            with
        { Url = "http://api.example.com/a" };
        yield return Row("REDIR-15: https to http to https flags only the middle hop (added)", 302, new Expect(RedirectDecisionKind.Follow) { NextUrl = "https://api.example.com/z", Downgraded = false }, "https://api.example.com/z")
            with
        { Visited = ["http://api.example.com/y"], Options = Opt(downgrade: true) };
        yield return Row("REDIR-15: the downgrade is judged on the hop, not the seed (added)", 302, new Expect(RedirectDecisionKind.Follow) { NextUrl = "http://api.example.com/z", Downgraded = false }, "http://api.example.com/z")
            with
        { Url = "http://api.example.com/y", SeedUrl = "https://api.example.com/a" };

        foreach (var status in new[] { 301, 302, 307, 308 })
        {
            yield return Row($"REDIR-6: a {status} over a single-use body fails", status, Boom<RedirectBodyNotReplayableException>())
                with
            { Method = "POST", Body = BodyKind.SingleUse, Options = Opt(methods: [Method.Post]) };
        }

        yield return Row("REDIR-6: a 303 is exempt from the replay gate", 303, Go($"{Host}/next"))
            with
        { Method = "POST", Body = BodyKind.SingleUse, Options = Opt(see: true) };
        yield return Row("REDIR-6: no body follows", 307, Go($"{Host}/next", "POST"))
            with
        { Method = "POST", Options = Opt(methods: [Method.Post]) };
        yield return Row("REDIR-6: a replayable body follows", 307, new Expect(RedirectDecisionKind.Follow) { NextMethod = "POST", SameBody = true })
            with
        { Method = "POST", Body = BodyKind.Bytes, Options = Opt(methods: [Method.Post]) };
        yield return Row("REDIR-6: a seekable FromStream body follows (3b hand-off, added)", 307, new Expect(RedirectDecisionKind.Follow) { NextMethod = "POST", SameBody = true })
            with
        { Method = "POST", Body = BodyKind.Seekable, Options = Opt(methods: [Method.Post]) };
        yield return Row("REDIR-6: a non-seekable FromStream body fails (added)", 307, Boom<RedirectBodyNotReplayableException>())
            with
        { Method = "POST", Body = BodyKind.SingleUse, Options = Opt(methods: [Method.Post]) };
        yield return Row("REDIR-15: the downgrade error wins over the replay error (design B)", 307, Boom<RedirectSchemeDowngradeException>(), "http://api.example.com/x")
            with
        { Method = "POST", Body = BodyKind.SingleUse, Options = Opt(methods: [Method.Post]) };
    }

    // header construction (L612-667), multi-valued headers (L885-943), body identity (L944-965): REDIR-7..11.
    private static IEnumerable<RedirectCase> Headers()
    {
        yield return Row("REDIR-7: Authorization is removed on a same-origin hop", 302,
            new Expect(RedirectDecisionKind.Follow) { NextUrl = $"{Host}/next", HeadersAbsent = ["Authorization"], HeadersPresent = ["Cookie", "Proxy-Authorization", "X-Keep"], CrossOrigin = false })
            with
        { RequestHeaders = s_creds };
        yield return Row("REDIR-7: every Authorization value is removed", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersAbsent = ["Authorization"] })
            with
        { RequestHeaders = [("Authorization", "a"), ("Authorization", "b")] };
        yield return Row("REDIR-9: Authorization, Cookie and Proxy-Authorization are removed cross-origin", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersAbsent = s_all3, HeadersPresent = ["X-Keep"], CrossOrigin = true }, "https://evil.example/x")
            with
        { RequestHeaders = s_creds };
        yield return Row("REDIR-10: Cookie and Proxy-Authorization survive same-origin", 307,
            new Expect(RedirectDecisionKind.Follow) { HeadersPresent = s_both, CrossOrigin = false })
            with
        { RequestHeaders = s_creds };
        yield return Row("REDIR-8: a port change alone is cross-origin", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersAbsent = s_all3, CrossOrigin = true }, "https://api.example.com:8443/x")
            with
        { RequestHeaders = s_creds };
        yield return Row("REDIR-8: a scheme change alone is cross-origin", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersAbsent = s_all3, CrossOrigin = true }, "https://api.example.com/x")
            with
        { Url = "http://api.example.com/a", RequestHeaders = s_creds };
        yield return Row("REDIR-8: an explicit default port is the same origin", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersPresent = s_both, CrossOrigin = false }, "https://API.example.com:443/x")
            with
        { RequestHeaders = s_creds };
        yield return Row("REDIR-8: an IDN host spelled two ways is one origin (added)", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersPresent = s_both, CrossOrigin = false }, "https://xn--bcher-kva.example/b")
            with
        { Url = "https://bücher.example/a", RequestHeaders = s_creds };
        yield return Row("REDIR-8: cross-origin is judged against the seed, not the previous hop", 302,
            new Expect(RedirectDecisionKind.Follow) { CrossOrigin = true }, "https://other.example/x")
            with
        { SeedUrl = "https://seed.example/a", Url = "https://other.example/a" };
        yield return Row("REDIR-8: a return to the seed origin is same-origin", 302,
            new Expect(RedirectDecisionKind.Follow) { CrossOrigin = false }, "https://seed.example/x")
            with
        { SeedUrl = "https://seed.example/a", Url = "https://other.example/a" };
        yield return Row("REDIR-11: a marker-shaped header is an ordinary header (P6b-23)", 302,
            new Expect(RedirectDecisionKind.Follow) { HeadersPresent = ["x-dexpace-internal-redirect-cross-origin"], CrossOrigin = false })
            with
        { RequestHeaders = [("x-dexpace-internal-redirect-cross-origin", "1")] };
        yield return Row("REDIR-3: a 307 carries the body instance", 307,
            new Expect(RedirectDecisionKind.Follow) { NextMethod = "PUT", SameBody = true })
            with
        { Method = "PUT", Body = BodyKind.Bytes, Options = Opt(methods: [Method.Put]) };
    }

    // 303 rebuild (L705-757): REDIR-5.
    private static IEnumerable<RedirectCase> SeeOther()
    {
        yield return Row("REDIR-5: a 303 is not followed by default", 303, Stop(RedirectStopReason.NotEligible));
        yield return Row("REDIR-5: a 303 is not followed on a POST by default", 303, Stop(RedirectStopReason.NotEligible))
            with
        { Method = "POST", Body = BodyKind.Bytes };

        foreach (var method in new[] { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE" })
        {
            var hasBody = method is "POST" or "PUT" or "PATCH";
            yield return Row(
                $"REDIR-5: an opted-in 303 on {method} becomes a body-less GET",
                303,
                new Expect(RedirectDecisionKind.Follow) { NextMethod = "GET", NoBody = true, NextUrl = $"{Host}/next" })
                with
            { Method = method, Body = hasBody ? BodyKind.Bytes : BodyKind.None, Options = Opt(see: true) };
        }

        yield return Row("REDIR-5: a 303 removes Content-* headers by case-insensitive prefix (added)", 303,
            new Expect(RedirectDecisionKind.Follow) { HeadersAbsent = ["content-type", "CONTENT-LENGTH", "cOnTeNt-Language", "Content-MD5"], HeadersPresent = ["Accept"] })
            with
        {
            Method = "POST",
            Body = BodyKind.Bytes,
            Options = Opt(see: true),
            RequestHeaders = [("content-type", "text/plain"), ("CONTENT-LENGTH", "1"), ("cOnTeNt-Language", "en"), ("Content-MD5", "x"), ("Accept", "*/*")],
        };
        yield return Row("REDIR-9: a cross-origin 303 also drops Cookie (added, Ruby round 1)", 303,
            new Expect(RedirectDecisionKind.Follow) { HeadersAbsent = s_all3, CrossOrigin = true }, "https://evil.example/x")
            with
        { Options = Opt(see: true), RequestHeaders = s_creds };
        yield return Row("REDIR-5: a predicate that follows a 303 gets the same rebuild", 303,
            new Expect(RedirectDecisionKind.Follow) { NextMethod = "GET", NoBody = true, PredicateCalls = 1 })
            with
        { Method = "POST", Body = BodyKind.Bytes, Options = Opt(predicate: static _ => true) };
    }
}
