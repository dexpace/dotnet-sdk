// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Driven by tests/vectors/sse/grammar.json, ported from nodejs-sdk@c0ff3fd packages/core/src/sse/parser.test.ts and the
// conformance fixtures of product-spec chapter 13.

using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The whole grammar over a stream: SSE-1 to SSE-14, SSE-16.</summary>
[Trait("Category", "Unit")]
public class ServerSentEventParserTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> CaseNames() => SseVectors.Names();

    [Fact]
    public void The_vector_corpus_covers_every_grammar_requirement()
    {
        var covered = SseVectors.Cases().SelectMany(c => c.Ids).ToHashSet(StringComparer.Ordinal);

        for (var id = 1; id <= 14; id++)
        {
            Assert.Contains($"SSE-{id}", covered);
        }

        Assert.Contains("SSE-16", covered);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Reader_matches_the_vector_table(string name)
    {
        var vector = SseVectors.Case(name);
        var expected = vector.ExpectedEvents();

        var blocking = new List<ServerSentEvent>();
        var syncReader = new ServerSentEventReader(new MemoryStream(vector.Bytes()));
        while (syncReader.ReadNext() is { } next)
        {
            blocking.Add(next);
        }

        var asynchronous = new List<ServerSentEvent>();
        var asyncReader = new ServerSentEventReader(new MemoryStream(vector.Bytes()));
        while (await asyncReader.ReadNextAsync(Token) is { } later)
        {
            asynchronous.Add(later);
        }

        Assert.Equal(expected, blocking);
        Assert.Equal(expected, asynchronous);
        Assert.Null(syncReader.ReadNext());
        Assert.Null(await asyncReader.ReadNextAsync(Token));
    }
}
