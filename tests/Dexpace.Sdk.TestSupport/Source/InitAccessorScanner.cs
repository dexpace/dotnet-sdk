// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Dexpace.Sdk.TestSupport.Source;

/// <summary>
/// Finds the validating <c>init</c> / <c>set</c> accessors of a source tree (SEAM-29, phase 11 task 2.5; phase 12 task 1.2). An accessor's parameter is called
/// <c>value</c> whatever the property is, so <c>ArgumentNullException.ThrowIfNull(value)</c> reported <c>ParamName == "value"</c>, which names nothing; the rule
/// is that the exception names the property (<c>ThrowIfNull(value, nameof(Property))</c>). The defect has two spellings: <c>nameof(value)</c> passed to an
/// exception, and a framework throw helper called on <c>value</c>, whose <c>ParamName</c> is the argument <em>expression</em> (<c>CallerArgumentExpression</c>),
/// so <c>"value"</c> again. A method whose own parameter is called <c>value</c> is not an accessor and is not reported.
/// </summary>
/// <remarks>
/// <para>
/// A source scan, not a syntax tree. An accessor is a line that is just <c>init</c> or <c>set</c>, or starts <c>init =&gt;</c> / <c>set =&gt;</c>; its body is the
/// braces that follow (or the rest of that line); its property is the last identifier of the nearest <c>public</c>, <c>internal</c>, <c>private</c> or
/// <c>protected</c> declaration above it that has no parenthesis before any <c>=</c>; the type is the file's name, one type per file. Inside the body, three
/// spellings reject a null (<see cref="SiteKind.Null"/>): <c>ArgumentNullException.ThrowIfNull(x)</c>, <c>ArgumentNullException.ThrowIfNull(x, nameof(P))</c> and
/// <c>new ArgumentNullException(nameof(P)...)</c>. Three more reject a range or a format (<see cref="SiteKind.Rejection"/>):
/// <c>ArgumentOutOfRangeException.ThrowIf*(x...)</c>, <c>ArgumentException.ThrowIfNullOrWhiteSpace(x...)</c> / <c>ThrowIfNullOrEmpty(x...)</c> (which also reject a
/// null, <see cref="Site.RejectsNull"/>, as an <see cref="ArgumentNullException"/> named the same way, so they are one rejection site and not two) and
/// <c>new Argument*Exception(..., nameof(P))</c>. A site about <c>value</c> itself is the property's; a site about anything else, or about <c>value[...]</c>,
/// is an <em>element</em> of the collection assigned. A call to a helper method of the type (<c>RequireDuration(value)</c>) is invisible to a line scan; its
/// accessors keep behavioural rows in the tests.
/// </para>
/// </remarks>
public static partial class InitAccessorScanner
{
    /// <summary>What a site rejects.</summary>
    public enum SiteKind
    {
        /// <summary>A null (the three <c>ArgumentNullException</c> spellings).</summary>
        Null,

        /// <summary>A value out of range or of the wrong format: a range helper, <c>ThrowIfNullOrWhiteSpace</c>, or a thrown <c>Argument*Exception</c> naming a parameter.</summary>
        Rejection,
    }

    /// <summary>One validating site in an accessor.</summary>
    /// <param name="File">The file, relative to the scanned root, with <c>/</c> separators.</param>
    /// <param name="Line">The 1-based line of the site.</param>
    /// <param name="Type">The file name without its extension.</param>
    /// <param name="Property">The property the accessor belongs to.</param>
    /// <param name="Element">Whether the site rejects a null element of the assigned collection rather than the value itself.</param>
    /// <param name="NamedParameter">The identifier inside the site's <c>nameof(...)</c>, or <see langword="null"/> when there is none.</param>
    /// <param name="Kind">Whether the site rejects a null or a range or format.</param>
    /// <param name="RejectsNull">Whether a <see cref="SiteKind.Rejection"/> site also rejects a null (<c>ThrowIfNullOrWhiteSpace</c> and <c>ThrowIfNullOrEmpty</c>).</param>
    public sealed record Site(string File, int Line, string Type, string Property, bool Element, string? NamedParameter, SiteKind Kind = SiteKind.Null, bool RejectsNull = false)
    {
        /// <summary>The name a test table uses for the site: <c>Type.Property</c>, or <c>Type.Property[]</c> for an element.</summary>
        public string Key => Element ? $"{Type}.{Property}[]" : $"{Type}.{Property}";

        /// <summary>Whether the exception names the property.</summary>
        public bool NamesItsProperty => NamedParameter == Property;
    }

    [GeneratedRegex(@"ArgumentNullException\.ThrowIfNull\(\s*(?<arg>[^,()]+?)\s*(?:,\s*nameof\((?<name>\w+)\))?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ThrowIfNull();

    [GeneratedRegex(@"ArgumentOutOfRangeException\.ThrowIf\w+\(\s*(?<arg>[^,()]+?)\s*[,)]", RegexOptions.CultureInvariant)]
    private static partial Regex ThrowIfOutOfRange();

    [GeneratedRegex(@"ArgumentException\.ThrowIfNullOr(?:WhiteSpace|Empty)\(\s*(?<arg>[^,()]+?)\s*[,)]", RegexOptions.CultureInvariant)]
    private static partial Regex ThrowIfNullOrBlank();

    [GeneratedRegex(@"new\s+(?!ArgumentNullException)Argument\w*Exception\(", RegexOptions.CultureInvariant)]
    private static partial Regex NewArgument();

    [GeneratedRegex(@"nameof\((?<name>\w+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex NameOf();

    [GeneratedRegex(@"new\s+ArgumentNullException\(\s*nameof\((?<name>\w+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex NewArgumentNull();

    /// <summary>Every null-rejecting site in an accessor under <paramref name="directory"/>, recursively, skipping <c>obj</c> and <c>bin</c>.</summary>
    /// <param name="directory">The directory to scan.</param>
    /// <returns>The sites, ordered by file and line.</returns>
    public static IReadOnlyList<Site> Find(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var sites = new List<Site>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var separator = Path.DirectorySeparatorChar;
            if (file.Contains($"{separator}obj{separator}", StringComparison.Ordinal) || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!IsAccessorKeyword(lines[i].Trim()) || PropertyAbove(lines, i) is not { } property)
                {
                    continue;
                }

                foreach (var (line, text) in Body(lines, i))
                {
                    AddSites(sites, relative, Path.GetFileNameWithoutExtension(file), property, line, text);
                }
            }
        }

        return sites;
    }

    private static void AddSites(List<Site> sites, string file, string type, string property, int line, string text)
    {
        foreach (Match call in ThrowIfNull().Matches(text))
        {
            var argument = call.Groups["arg"].Value;
            var name = call.Groups["name"].Success ? call.Groups["name"].Value : null;
            sites.Add(new Site(file, line, type, property, argument != "value", name == "value" ? null : name));
        }

        foreach (Match created in NewArgumentNull().Matches(text))
        {
            var element = text[..created.Index].Contains("value[", StringComparison.Ordinal);
            var name = created.Groups["name"].Value;
            sites.Add(new Site(file, line, type, property, element, name == "value" ? null : name));
        }

        AddRejections(sites, file, type, property, line, text);
    }

    // The range and format spellings: a framework helper on `value` (or on an element), and a thrown Argument*Exception that names a parameter.
    private static void AddRejections(List<Site> sites, string file, string type, string property, int line, string text)
    {
        foreach (var (pattern, rejectsNull) in new[] { (ThrowIfOutOfRange(), false), (ThrowIfNullOrBlank(), true) })
        {
            foreach (Match call in pattern.Matches(text))
            {
                sites.Add(new Site(file, line, type, property, call.Groups["arg"].Value != "value", NamedAfter(text, call.Index), SiteKind.Rejection, rejectsNull));
            }
        }

        foreach (Match created in NewArgument().Matches(text))
        {
            var element = text[..created.Index].Contains("value[", StringComparison.Ordinal);
            sites.Add(new Site(file, line, type, property, element, NamedAfter(text, created.Index), SiteKind.Rejection));
        }
    }

    // The `nameof(X)` a call passes (the last on its line), or null when it passes none or passes `nameof(value)`, which names nothing.
    private static string? NamedAfter(string text, int from)
    {
        var names = NameOf().Matches(text, from);
        return names.Count == 0 || names[^1].Groups["name"].Value == "value" ? null : names[^1].Groups["name"].Value;
    }

    private static bool IsAccessorKeyword(string trimmed) =>
        trimmed is "init" or "set" || trimmed.StartsWith("init =>", StringComparison.Ordinal) || trimmed.StartsWith("set =>", StringComparison.Ordinal);

    // The lines of an accessor's body, from the keyword line to the brace that closes it (or just the keyword line for an expression body).
    private static IEnumerable<(int Line, string Text)> Body(string[] lines, int keyword)
    {
        if (lines[keyword].Contains("=>", StringComparison.Ordinal))
        {
            yield return (keyword + 1, lines[keyword]);
            yield break;
        }

        var depth = 0;
        var opened = false;
        for (var i = keyword + 1; i < lines.Length; i++)
        {
            yield return (i + 1, lines[i]);
            foreach (var c in WithoutStringsAndComments(lines[i]))
            {
                if (c == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (c == '}')
                {
                    depth--;
                }
            }

            if (opened && depth <= 0)
            {
                yield break;
            }
        }
    }

    private static string WithoutStringsAndComments(string line)
    {
        var kept = new System.Text.StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                break;
            }
            else
            {
                kept.Append(c);
            }
        }

        return kept.ToString();
    }

    private static string? PropertyAbove(string[] lines, int accessor)
    {
        for (var i = accessor - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (!(line.StartsWith("public ", StringComparison.Ordinal) || line.StartsWith("internal ", StringComparison.Ordinal)
                || line.StartsWith("private ", StringComparison.Ordinal) || line.StartsWith("protected ", StringComparison.Ordinal)))
            {
                continue;
            }

            var declaration = line.Split('=')[0];
            return declaration.Contains('(', StringComparison.Ordinal) ? null : declaration.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[^1];
        }

        return null;
    }
}
