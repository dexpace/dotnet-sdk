// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// NFR-12 (phase 12 task 1.1; design V6, V7): an assembly has at most one type with <c>[LoggerMessage]</c> methods. The <c>LoggerMessage</c> source generator emits one
/// <c>LoggerMessage.g.cs</c> per assembly and, with two such classes, orders them differently from one compilation to the next, so the same commit packs different
/// bytes and the <c>reproducible packages</c> job goes red. A green run of the pack script cannot show the fix (a nondeterministic order passes by chance), so this
/// source scan is the standing gate; <see cref="LoggerMessageScanner"/> says what counts as a site. The scan is a source scan with a self-test over inline snippets
/// that proves it can fail, in the style of <see cref="FatalFilterArchitectureTests"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoggerMessageDeterminismArchitectureTests
{
    [Fact]
    public void Each_library_has_at_most_one_type_with_generated_log_methods()
    {
        var violations = new List<string>();
        foreach (var (library, sites) in SitesByLibrary())
        {
            var types = LoggerMessageScanner.Types(sites);
            if (types.Count > 1)
            {
                var where = string.Join(", ", sites.Select(static site => $"{site.File}:{site.Line}"));
                violations.Add($"{library} has [LoggerMessage] methods in {types.Count} types ({string.Join(", ", types)}; {where}): the generator orders the classes of one assembly "
                    + "nondeterministically, so its packs are not reproducible (NFR-12, V6). Hold every generated log method in one type.");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void The_scan_sees_the_real_generator_sites()
    {
        var byLibrary = SitesByLibrary().ToDictionary(static pair => pair.Library, static pair => pair.Sites, StringComparer.Ordinal);

        // Not vacuous: Core's proxy-resolution log is found, and the transport has at least one site.
        Assert.Contains(byLibrary["Dexpace.Sdk.Core"], static site => site.File.EndsWith("Internal/ProxyResolutionLog.cs", StringComparison.Ordinal) && site.Type == "ProxyResolutionLog");
        Assert.NotEmpty(byLibrary["Dexpace.Sdk.Http.SystemNet"]);
    }

    [Theory]
    [InlineData("internal static partial class A\n{\n    [LoggerMessage(EventId = 1)]\n    static partial void M(ILogger l);\n}\ninternal static partial class B\n{\n    [LoggerMessage(EventId = 2)]\n    static partial void N(ILogger l);\n}\n", 2)]
    [InlineData("internal sealed partial class A\n{\n    [LoggerMessage(EventId = 1)]\n    static partial void M(ILogger l);\n    [LoggerMessage(\n        EventId = 2)]\n    static partial void N(ILogger l);\n    [LoggerMessage(EventId = 3)]\n    static partial void O(ILogger l);\n}\n", 1)]
    [InlineData("internal static class A\n{\n    static readonly Action<ILogger> s_m = LoggerMessage.Define(LogLevel.Debug, new EventId(1), \"x\");\n}\ninternal static class B\n{\n    static readonly Action<ILogger> s_n = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2), \"{X}\");\n}\n", 0)]
    [InlineData("internal static partial class A\n{\n    // [LoggerMessage(EventId = 1)]\n    /// [LoggerMessage(EventId = 2)]\n}\ninternal static partial class B\n{\n    [LoggerMessage(EventId = 3)]\n    static partial void N(ILogger l);\n}\n", 1)]
    [InlineData("public readonly record struct R\n{\n    [LoggerMessage(EventId = 1)]\n    static partial void M(ILogger l);\n}\npublic record class S\n{\n    [LoggerMessage(EventId = 2)]\n    static partial void N(ILogger l);\n}\n", 2)]
    public void The_scanner_flags_two_types_and_ignores_define_and_comments(string source, int types)
    {
        Assert.Equal(types, LoggerMessageScanner.Types(LoggerMessageScanner.Sites("snippet.cs", source)).Count);
    }

    [Fact]
    public void A_partial_class_split_across_two_files_is_one_type()
    {
        const string First = "internal static partial class Log\n{\n    [LoggerMessage(EventId = 1)]\n    static partial void M(ILogger l);\n}\n";
        const string Second = "internal static partial class Log\n{\n    [LoggerMessage(EventId = 2)]\n    static partial void N(ILogger l);\n}\n";

        var sites = LoggerMessageScanner.Sites("one.cs", First).Concat(LoggerMessageScanner.Sites("two.cs", Second)).ToList();

        Assert.Equal(["Log"], LoggerMessageScanner.Types(sites));
        Assert.Equal(2, sites.Count);
    }

    [Fact]
    public void The_scanner_reports_the_line_of_the_attribute()
    {
        const string Source = "class C\n{\n    [LoggerMessage(\n        EventId = 1)]\n    static partial void M(ILogger l);\n}\n";

        var site = Assert.Single(LoggerMessageScanner.Sites("snippet.cs", Source));

        Assert.Equal(3, site.Line);
        Assert.Equal("C", site.Type);
    }

    private static List<(string Library, List<LoggerMessageScanner.Site> Sites)> SitesByLibrary()
    {
        var source = SourceTreeRoot();
        var result = new List<(string, List<LoggerMessageScanner.Site>)>();
        foreach (var directory in Directory.EnumerateDirectories(source, "Dexpace.Sdk.*").Order(StringComparer.Ordinal))
        {
            var sites = new List<LoggerMessageScanner.Site>();
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                sites.AddRange(LoggerMessageScanner.Sites(Path.GetRelativePath(source, file).Replace('\\', '/'), File.ReadAllText(file)));
            }

            result.Add((Path.GetFileName(directory), sites));
        }

        return result;
    }

    private static string SourceTreeRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(Path.Combine(candidate, "Dexpace.Sdk.Core")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The src tree was not found above the test output.");
    }
}
