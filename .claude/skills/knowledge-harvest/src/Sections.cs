// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgeHarvest;

/// <summary>One markdown section: a 1-indexed inclusive line range and its UTF-8 byte size.</summary>
internal sealed record Section(string? Heading, int[] Lines, int Bytes);

/// <summary>Splits markdown into the sections a chunk may be cut along.</summary>
internal static partial class Sections
{
    /// <summary>The heading levels tried, shallowest first.</summary>
    public static readonly IReadOnlyList<int> SplitLevels = [2, 3];

    [GeneratedRegex(@"^\s*(`{3,}|~{3,})", RegexOptions.ECMAScript)]
    private static partial Regex Fence();

    /// <summary>
    /// Splits at headings of exactly <paramref name="level"/>. Content before the first heading becomes a
    /// leading section with a null heading, so the sections always tile the whole file: a split must never
    /// silently drop a preamble. Headings inside fenced code blocks are ignored.
    /// </summary>
    public static List<Section> Parse(string text, int level = 2)
    {
        var lines = SplitKeepingEnds(text);
        if (lines.Count == 0)
        {
            return [];
        }

        var heading = new Regex("^#{" + level + @"}(?!#)\s*(.*?)\s*$", RegexOptions.ECMAScript);
        var starts = new List<(int Index, string? Heading)>();
        string? fence = null;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (fence is not null)
            {
                if (line.TrimStart().StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = null;
                }

                continue;
            }

            var opening = Fence().Match(line);
            if (opening.Success)
            {
                fence = opening.Groups[1].Value[..3];
                continue;
            }

            var match = heading.Match(line.TrimEnd('\r', '\n'));
            if (match.Success)
            {
                starts.Add((index, match.Groups[1].Value));
            }
        }

        if (starts.Count == 0 || starts[0].Index != 0)
        {
            starts.Insert(0, (0, null));
        }

        var sections = new List<Section>();
        for (var position = 0; position < starts.Count; position++)
        {
            var start = starts[position].Index;
            var end = position + 1 < starts.Count ? starts[position + 1].Index - 1 : lines.Count - 1;
            if (end < start)
            {
                continue;
            }

            var bytes = Encoding.UTF8.GetByteCount(string.Concat(lines.Skip(start).Take(end - start + 1)));
            sections.Add(new Section(starts[position].Heading, [start + 1, end + 1], bytes));
        }

        return sections;
    }

    /// <summary>
    /// Picks the shallowest heading level that actually divides the file. Many documents use a single
    /// <c>##</c> as the title and <c>###</c> for real sections; splitting at <c>##</c> there yields one
    /// section covering everything, which would make an oversized file unsplittable.
    /// </summary>
    public static (int Level, List<Section> Sections) Choose(string text)
    {
        var result = (Level: SplitLevels[0], Sections: Parse(text, SplitLevels[0]));
        foreach (var level in SplitLevels)
        {
            var sections = Parse(text, level);
            if (sections.Count > 1)
            {
                return (level, sections);
            }

            if (result.Sections.Count == 0)
            {
                result = (level, sections);
            }
        }

        return result;
    }

    /// <summary>The line count a source has, by the same <c>\n</c>-terminated reckoning evidence uses.</summary>
    public static int CountLines(string text) => SplitKeepingEnds(text).Count;

    private static List<string> SplitKeepingEnds(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines.Add(text[start..(index + 1)]);
                start = index + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }
}
