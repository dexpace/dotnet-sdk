// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>One expected event of a grammar vector; an absent <c>data</c> is the empty list (P7b-7).</summary>
internal sealed record GrammarEvent(string? Id, string? Event, string[]? Data, string? Comment, int? RetryMs)
{
    internal ServerSentEvent ToEvent() => new()
    {
        Id = Id,
        Event = Event,
        Data = Data ?? [],
        Comment = Comment,
        Retry = RetryMs is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null,
    };
}

/// <summary>One case of <c>tests/vectors/sse/grammar.json</c>: the bytes of a whole stream and the events it yields.</summary>
internal sealed record GrammarCase(string Name, string[] Ids, string? Input, string? InputHex, GrammarEvent[] Events)
{
    internal byte[] Bytes() =>
        InputHex is not null ? Convert.FromHexString(InputHex) : Encoding.UTF8.GetBytes(Input ?? string.Empty);

    internal List<ServerSentEvent> ExpectedEvents() => [.. Events.Select(e => e.ToEvent())];
}

/// <summary>Loads the shared grammar vectors (roadmap constraint 10) for every SSE test that walks the corpus.</summary>
internal static class SseVectors
{
    private const string Path = "sse/grammar.json";

    internal static IReadOnlyList<GrammarCase> Cases() => VectorFile.Load<GrammarCase>(Path);

    internal static GrammarCase Case(string name) => Cases().Single(c => c.Name == name);

    internal static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var vector in Cases())
        {
            data.Add(vector.Name);
        }

        return data;
    }
}
