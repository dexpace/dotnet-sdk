// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>
/// The card's one-<c>[Theory]</c> decision table (REDIR-1 to REDIR-21, REDIR-26; P6b-26). The decider is pure, so a row
/// needs no pipeline. Rows are keyed by name because a public test method cannot take an internal type (R9).
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedirectDecisionMatrixTests
{
    public static TheoryData<string> Cases() => [.. RedirectCases.Names()];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Decide_matches_the_table(string name)
    {
        var row = RedirectCases.Named(name);
        var (response, chain, body, options, calls) = Arrange(row);
        using (response)
        {
            var decision = RedirectDecider.Decide(response, chain, options);

            Assert.Equal(row.Expect.Kind, decision.Kind);
            switch (decision.Kind)
            {
                case RedirectDecisionKind.ReturnCurrent:
                    Assert.Equal(row.Expect.Reason, decision.Reason);
                    if (row.Expect.Malformed is { } malformed)
                    {
                        Assert.Equal(malformed, decision.MalformedRaw is not null);
                    }

                    break;
                case RedirectDecisionKind.Fail:
                    Assert.IsType(row.Expect.ExceptionType!, decision.Exception);
                    break;
                default:
                    AssertFollow(row.Expect, decision, body);
                    break;
            }

            if (row.Expect.PredicateCalls is { } expected)
            {
                Assert.Equal(expected, calls());
            }
        }
    }

    [Fact]
    public void Every_row_name_is_unique_and_the_table_is_not_empty()
    {
        var names = RedirectCases.Names();

        Assert.NotEmpty(names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertFollow(Expect expect, RedirectDecision decision, RequestBody? firstBody)
    {
        var next = decision.Next!;
        if (expect.NextMethod is not null)
        {
            Assert.Equal(Method.Of(expect.NextMethod), next.Method);
        }

        if (expect.NextUrl is not null)
        {
            Assert.Equal(expect.NextUrl, next.Url.AbsoluteUri);
            Assert.Equal(expect.NextUrl, decision.Target!.AbsoluteUri);
        }

        foreach (var header in expect.HeadersPresent)
        {
            Assert.True(next.Headers.Contains(header), $"{header} should be present");
        }

        foreach (var header in expect.HeadersAbsent)
        {
            Assert.False(next.Headers.Contains(header), $"{header} should be absent");
        }

        if (expect.SameBody == true)
        {
            Assert.NotNull(firstBody);
            Assert.Same(firstBody, next.Body);
        }

        if (expect.NoBody == true)
        {
            Assert.Null(next.Body);
        }

        if (expect.CrossOrigin is { } cross)
        {
            Assert.Equal(cross, decision.CrossOrigin);
        }

        if (expect.Downgraded is { } downgraded)
        {
            Assert.Equal(downgraded, decision.Downgraded);
        }
    }

    private static (Response Response, RedirectChain Chain, RequestBody? Body, RedirectOptions Options, Func<int> Calls) Arrange(RedirectCase row)
    {
        var requestHeaders = new Headers.Builder();
        foreach (var (name, value) in row.RequestHeaders)
        {
            requestHeaders.Add(name, value);
        }

        RequestBody? body = row.Body switch
        {
            BodyKind.Bytes => RequestBody.FromBytes(new byte[] { 1, 2, 3 }),
            BodyKind.SingleUse => RequestBody.FromStream(new MemoryStream([1, 2, 3])),
            BodyKind.Seekable => RequestBody.FromStream(new MemoryStream([1, 2, 3]), contentLength: 3),
            _ => null,
        };
        var first = new Request(Method.Of(row.Method), new Uri(row.Url), requestHeaders.Build(), body);
        var seed = Request.Get(row.SeedUrl ?? row.Url);
        var chain = new RedirectChain(first, TestContexts.For(seed));

        for (var i = 1; i <= row.Followed; i++)
        {
            var hop = new Uri(first.Url, $"/hop{i}");
            chain.Advance(Request.Get(hop.AbsoluteUri), hop);
        }

        foreach (var visited in row.Visited)
        {
            chain.Advance(Request.Get(visited), new Uri(visited));
        }

        var responseHeaders = new Headers.Builder();
        foreach (var value in row.LocationValues)
        {
            responseHeaders.AddInbound("Location", value);
        }

        foreach (var (name, value) in row.ResponseHeaders)
        {
            responseHeaders.Add(name, value);
        }

        var response = TestResponses.Create(Status.FromCode(row.Status), chain.Current, responseHeaders.Build());

        var count = 0;
        var options = row.Options;
        if (options.Predicate is { } predicate)
        {
            options = options with
            {
                Predicate = condition =>
                {
                    count++;
                    return predicate(condition);
                },
            };
        }

        return (response, chain, body, options, () => count);
    }
}
