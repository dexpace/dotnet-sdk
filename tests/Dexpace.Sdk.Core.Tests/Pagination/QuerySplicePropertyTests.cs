// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/pagination/query-splice.property.test.ts (three properties). Node
// uses fast-check; this port uses a seeded System.Random generator, 500 cases per property, so no package is added and
// no lock file moves (design P7c-18). The seed is in each test name and in every failure message, so a failure replays.
// The Node generator excludes the characters the WHATWG query encode set rewrites; that exclusion is a JavaScript
// host fact and has no .NET counterpart, so the alphabets below carry no such exclusion.

using System.Text;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-21 and PAGE-22 as properties over seeded random input (design P7c-18).</summary>
[Trait("Category", "Unit")]
public sealed class QuerySplicePropertyTests
{
    private const int Seed = 20261009;
    private const int Cases = 500;

    // Letters, digits and the punctuation a real server sends; "%41" is a valid escape that System.Uri canonicalises.
    private static readonly string[] s_segmentAlphabet =
        ["a", "b", "z", "0", "9", "A", "Z", "%41", "+", ":", "~", "-", "_", ".", "=", "/", "@"];

    // Ordinary text, reserved characters, whitespace, controls, non-ASCII and a surrogate pair; never a lone surrogate.
    private static readonly string[] s_wellFormedAlphabet =
    [
        "a", "Z", "0", " ", "+", "&", "=", "%", "#", "?", "/", ":", ";", ",", "'", "\"", "<", ">", "\\", "\t", "\r", "\n",
        "\0", "\u00E9", "\u20AC", "\u2028", "\U0001F600", "%41", "%2F", "%zz",
    ];

    // Well-formed text plus unpaired surrogates and percent garbage: what a hostile server can put in a cursor.
    private static readonly string[] s_hostileAlphabet =
    [
        "a", "b", " ", "=", "&", "%", "+", "%G1", "%", "%E2%80", "\U0001F600", "\uD800", "\uDBFF", "\uDC00", "\uDFFF",
    ];

    private static string Random(Random rng, string[] alphabet, int minLength, int maxLength)
    {
        var length = rng.Next(minLength, maxLength + 1);
        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[rng.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    private static List<string> RandomSegments(Random rng)
    {
        var segments = new List<string>();
        var count = rng.Next(0, 7);
        for (var i = 0; i < count; i++)
        {
            var name = new string(Enumerable.Range(0, rng.Next(1, 7)).Select(_ => (char)('a' + rng.Next(26))).ToArray());
            segments.Add($"{name}={Random(rng, s_segmentAlphabet, 0, 10)}");
        }

        return segments;
    }

    [Fact]
    public void Untargeted_segments_are_byte_identical_after_a_set_seed_20261009()
    {
        var rng = new Random(Seed);
        for (var i = 0; i < Cases; i++)
        {
            var segments = RandomSegments(rng);
            var untargeted = segments.Where(s => !s.StartsWith("page=", StringComparison.Ordinal)).ToList();
            var url = new Uri("https://h/p?" + string.Join('&', [.. untargeted, "page=1"]), UriKind.Absolute);
            var before = url.Query.TrimStart('?').Split('&');

            var result = QuerySplice.Set(url, "page", Random(rng, s_wellFormedAlphabet, 1, 12));

            var after = result.Query.TrimStart('?').Split('&');
            var context = $"seed {Seed}, case {i}: {url.Query} -> {result.Query}";
            Assert.True(after.Length == before.Length, context);
            for (var s = 0; s < untargeted.Count; s++)
            {
                Assert.True(after[s] == before[s], context);
            }
        }
    }

    [Fact]
    public void A_name_outside_the_alphabet_only_ever_appends_seed_20261009()
    {
        var rng = new Random(Seed);
        for (var i = 0; i < Cases; i++)
        {
            var segments = RandomSegments(rng);
            var url = new Uri("https://h/p" + (segments.Count == 0 ? string.Empty : "?" + string.Join('&', segments)), UriKind.Absolute);

            var result = QuerySplice.Set(url, "zz-target", "v");

            var expected = url.Query.Length == 0 ? "?zz-target=v" : url.Query + "&zz-target=v";
            Assert.True(result.Query == expected, $"seed {Seed}, case {i}: {url.Query} -> {result.Query}");
        }
    }

    [Fact]
    public void Write_then_read_is_the_identity_for_any_well_formed_value_seed_20261009()
    {
        var rng = new Random(Seed);
        for (var i = 0; i < Cases; i++)
        {
            var name = Random(rng, s_wellFormedAlphabet, 1, 6);
            var value = Random(rng, s_wellFormedAlphabet, 0, 20);
            var url = QuerySplice.Set(new Uri("https://h/p?a=1&b=2", UriKind.Absolute), name, value);

            Assert.True(QuerySplice.Get(url, name) == value, $"seed {Seed}, case {i}: name '{Describe(name)}' value '{Describe(value)}' -> {url.Query}");
            Assert.True(
                QuerySplice.Get(QuerySplice.Set(url, name, null), name) is null,
                $"seed {Seed}, case {i}: removal left the parameter in {url.Query}");
        }
    }

    [Fact]
    public void Nothing_but_ArgumentException_escapes_for_any_server_shaped_string_seed_20261009()
    {
        var rng = new Random(Seed);
        for (var i = 0; i < Cases; i++)
        {
            var name = Random(rng, s_hostileAlphabet, 0, 8);
            var value = Random(rng, s_hostileAlphabet, 0, 8);
            var hostileQuery = Random(rng, s_hostileAlphabet, 0, 24);
            var context = $"seed {Seed}, case {i}: name '{Describe(name)}' value '{Describe(value)}' query '{Describe(hostileQuery)}'";

            AssertOnlyArgumentExceptions(() => QuerySplice.Get(new Uri("https://h/p?a=1"), name), context);
            AssertOnlyArgumentExceptions(() =>
            {
                var written = QuerySplice.Set(new Uri("https://h/p?a=1"), name, value);
                Assert.True(QuerySplice.Get(written, name) == value, context);
            }, context);

            // A hostile query on the URL itself: only a URI System.Uri accepts can reach the splice.
            if (Uri.TryCreate("https://h/p?" + hostileQuery, UriKind.Absolute, out var hostile))
            {
                AssertOnlyArgumentExceptions(() => QuerySplice.Get(hostile, "page"), context);
                AssertOnlyArgumentExceptions(() => QuerySplice.Set(hostile, "page", "2"), context);
            }
        }
    }

    // Runs the action; the only exception it may throw is ArgumentException (an empty name, a lone surrogate).
    private static void AssertOnlyArgumentExceptions(Action action, string context)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            // The one sanctioned failure.
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail($"{context}: {ex.GetType().Name} escaped the splice.");
        }
    }

    // Escapes control and surrogate characters so a failure message is readable and survives the test report.
    private static string Describe(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            builder.Append(char.IsControl(ch) || char.IsSurrogate(ch) ? $"\\u{(int)ch:X4}" : ch.ToString());
        }

        return builder.ToString();
    }
}
