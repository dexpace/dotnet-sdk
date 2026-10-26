// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// A line scanner for the determinism gate (NFR-12, phase 12 task 1.1, reading R4): it finds every <c>[LoggerMessage(</c> attribute site in a source text and
/// the type that holds it. The <c>LoggerMessage</c> source generator writes one <c>LoggerMessage.g.cs</c> per assembly and orders the classes in it by an
/// order that is not stable between compilations when an assembly has more than one such class (V6), so two builds of the same commit pack different bytes.
/// </summary>
/// <remarks>
/// A site is a line whose first token is <c>[LoggerMessage(</c>; a line comment (<c>//</c>, <c>///</c>) holds none, and <c>LoggerMessage.Define&lt;…&gt;(</c> is not
/// generator input and is not a site. The holding type is the nearest preceding <c>class</c>, <c>struct</c> or <c>record</c> declaration on a non-comment line (the
/// repository convention is one type per file), named by its simple name, so a <c>partial</c> type split across files is one type.
/// </remarks>
internal static partial class LoggerMessageScanner
{
    /// <summary>One <c>[LoggerMessage(</c> attribute site.</summary>
    /// <param name="File">The file label given to <see cref="Sites"/>.</param>
    /// <param name="Line">The 1-based line of the attribute.</param>
    /// <param name="Type">The simple name of the nearest preceding type declaration, or <c>&lt;none&gt;</c> when there is none.</param>
    internal sealed record Site(string File, int Line, string Type);

    [GeneratedRegex(@"^\s*(?:(?:public|internal|private|protected|file|static|sealed|abstract|partial|readonly|unsafe|ref|new)\s+)*(?:class|struct|record(?:\s+(?:class|struct))?)\s+(?<name>\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclaration();

    /// <summary>Every attribute site in <paramref name="text"/>.</summary>
    /// <param name="file">A label for the file, carried into each site.</param>
    /// <param name="text">The C# source.</param>
    /// <returns>The sites, in source order.</returns>
    internal static List<Site> Sites(string file, string text)
    {
        var sites = new List<Site>();
        var type = "<none>";
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var declaration = TypeDeclaration().Match(line);
            if (declaration.Success)
            {
                type = declaration.Groups["name"].Value;
            }
            else if (trimmed.StartsWith("[LoggerMessage(", StringComparison.Ordinal))
            {
                sites.Add(new Site(file, lineNumber, type));
            }
        }

        return sites;
    }

    /// <summary>The distinct types that hold at least one of <paramref name="sites"/>, in first-seen order.</summary>
    /// <param name="sites">Sites from one library.</param>
    /// <returns>The simple type names.</returns>
    internal static List<string> Types(IEnumerable<Site> sites) => sites.Select(static site => site.Type).Distinct(StringComparer.Ordinal).ToList();
}
