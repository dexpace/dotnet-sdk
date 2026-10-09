// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Replaces Node's line-reader.property.test.ts and parser.property.test.ts (nodejs-sdk@c0ff3fd): split-invariance is
// checked exhaustively over the vector corpus rather than sampled, and the round trip is seeded so a failure reproduces
// from the seed it prints (P7b-21).

using System.Text;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>Where the read boundaries fall must never change the events: SSE-2, SSE-12, SSE-14.</summary>
[Trait("Category", "Unit")]
public class ServerSentEventChunkSplitTests
{
    private const int ThreeWayLimit = 24;

    private static readonly string[] s_terminators = ["\n", "\r", "\r\n"];

    // Code points a generated value is drawn from: ASCII, space and tab, a colon, NUL (data/event/comment only), the BOM,
    // NEL and the Unicode line separators (which are not SSE terminators), Latin-1, CJK and a supplementary pair.
    private static readonly int[] s_codePoints =
        [.. Enumerable.Range(0x20, 0x5F), 0x09, 0x3A, 0x00, 0xFEFF, 0x85, 0x2028, 0x2029, 0xE9, 0xA0, 0x4E16, 0x754C, 0x1F600];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> CaseNames() => SseVectors.Names();

    public static TheoryData<int> Seeds() => [.. Enumerable.Range(1, 25)];

    private static async Task<List<ServerSentEvent>> ParseAsync(Stream source, bool useAsync)
    {
        var reader = new ServerSentEventReader(source);
        var events = new List<ServerSentEvent>();
        while (true)
        {
            var next = useAsync ? await reader.ReadNextAsync(Token) : reader.ReadNext();
            if (next is null)
            {
                return events;
            }

            events.Add(next);
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Every_two_chunk_split_of_every_vector_yields_the_same_events(string name)
    {
        var vector = SseVectors.Case(name);
        var bytes = vector.Bytes();
        var expected = vector.ExpectedEvents();

        for (var cut = 1; cut < bytes.Length; cut++)
        {
            foreach (var useAsync in new[] { false, true })
            {
                var actual = await ParseAsync(new SplitReadStream(bytes, cut), useAsync);

                if (!expected.SequenceEqual(actual))
                {
                    Assert.Fail($"{name}: cut at {cut} (async={useAsync}) gave [{string.Join("; ", actual)}], expected [{string.Join("; ", expected)}].");
                }
            }
        }
    }

    [Fact]
    public async Task Every_three_chunk_split_of_a_vector_up_to_24_bytes_yields_the_same_events()
    {
        var short24 = SseVectors.Cases().Where(c => c.Bytes().Length <= ThreeWayLimit).ToList();
        Assert.True(short24.Count >= 20, $"only {short24.Count} vectors are short enough for an exhaustive three-way split.");

        foreach (var vector in short24)
        {
            var bytes = vector.Bytes();
            var expected = vector.ExpectedEvents();
            for (var first = 1; first < bytes.Length; first++)
            {
                for (var second = first + 1; second < bytes.Length; second++)
                {
                    var actual = await ParseAsync(new SplitReadStream(bytes, first, second), useAsync: second % 2 == 0);

                    if (!expected.SequenceEqual(actual))
                    {
                        Assert.Fail($"{vector.Name}: cuts at {first},{second} gave [{string.Join("; ", actual)}], expected [{string.Join("; ", expected)}].");
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task A_seeded_round_trip_serialises_and_parses_back_to_the_same_events(int seed)
    {
        var random = new Random(seed);
        var count = random.Next(1, 7);
        var generated = Enumerable.Range(0, count).Select(_ => GenerateEvent(random)).ToList();
        var wire = Encoding.UTF8.GetBytes(Serialise(generated, random));

        var variants = new Dictionary<string, Stream>
        {
            ["whole"] = new MemoryStream(wire),
            ["byte-at-a-time"] = new ChunkedReadStream(wire, 1),
            ["random-cuts"] = new SplitReadStream(wire, RandomCuts(random, wire.Length)),
        };

        foreach (var (label, stream) in variants)
        {
            var actual = await ParseAsync(stream, useAsync: seed % 2 == 0);

            if (!generated.SequenceEqual(actual))
            {
                Assert.Fail($"seed {seed} ({label}): wire {Visible(wire)} parsed to [{string.Join("; ", actual)}], expected [{string.Join("; ", generated)}].");
            }
        }
    }

    private static int[] RandomCuts(Random random, int length)
    {
        if (length < 3)
        {
            return [];
        }

        var first = random.Next(1, length - 1);
        var second = random.Next(first + 1, length);
        return [first, second];
    }

    private static string Visible(byte[] wire) =>
        Encoding.UTF8.GetString(wire).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static ServerSentEvent GenerateEvent(Random random)
    {
        var data = Enumerable.Range(0, random.Next(0, 5)).Select(_ => RandomText(random, allowNul: true)).ToList();
        var generated = new ServerSentEvent
        {
            Id = random.Next(2) == 0 ? RandomText(random, allowNul: false) : null,
            Event = random.Next(2) == 0 ? RandomText(random, allowNul: true) : null,
            Data = data,
            Comment = random.Next(10) < 3 ? RandomText(random, allowNul: true) : null,
            Retry = random.Next(10) < 3 ? TimeSpan.FromMilliseconds(random.NextInt64(0, int.MaxValue + 1L)) : null,
        };

        // Every generated event sets at least one field, or it would not survive as an event at all.
        return generated.IsEmpty ? generated with { Data = [RandomText(random, allowNul: true)] } : generated;
    }

    private static string RandomText(Random random, bool allowNul)
    {
        var length = random.Next(0, 12);
        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            int codePoint;
            do
            {
                codePoint = s_codePoints[random.Next(s_codePoints.Length)];
            }
            while (codePoint == 0 && !allowNul);

            builder.Append(char.ConvertFromUtf32(codePoint));
        }

        return builder.ToString();
    }

    private static string Serialise(List<ServerSentEvent> events, Random random)
    {
        var builder = new StringBuilder();
        if (random.Next(2) == 0)
        {
            builder.Append('﻿');
        }

        foreach (var ev in events)
        {
            var lines = new List<string>();
            if (ev.Comment is not null)
            {
                lines.Add(": " + ev.Comment);
            }

            if (ev.Id is not null)
            {
                lines.Add("id: " + ev.Id);
            }

            if (ev.Event is not null)
            {
                lines.Add("event: " + ev.Event);
            }

            if (ev.Retry is { } retry)
            {
                lines.Add("retry: " + ((long)retry.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            lines.AddRange(ev.Data.Select(d => "data: " + d));
            var previous = string.Empty;
            foreach (var line in lines)
            {
                previous = s_terminators[random.Next(s_terminators.Length)];
                builder.Append(line).Append(previous);
            }

            // The blank line ends the block. A CR then a bare LF would fuse into one CRLF and swallow it, so after a CR
            // the blank line takes a CR or a CRLF.
            var blank = s_terminators[random.Next(s_terminators.Length)];
            builder.Append(previous == "\r" && blank == "\n" ? "\r" : blank);
        }

        return builder.ToString();
    }
}
