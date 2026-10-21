// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// RECOV-2, RETRY-25, XCUT-20 (phase 11, task 1.4; design §10 entry 12): no catch-all in a runtime library swallows a fatal exception. <c>CA1031</c> is silent
/// on <c>catch (Exception ex) when (true)</c> and on a catch that rethrows, so the build cannot enforce this (V16); this is the gate. Two rules, over every
/// <c>.cs</c> file of the four runtime libraries: an unfiltered catch-all must end its handler in <c>throw;</c> (or rethrow a fatal exception), and a filtered one
/// must have a <c>when</c> that excludes fatal exceptions (<see cref="FatalFilterScanner"/> says exactly which forms count). The scan is a source scan with a
/// self-test over inline snippets that proves it can fail, in the style of <see cref="BannedSymbolsSiteTests"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FatalFilterArchitectureTests
{
    // The runtime libraries. The conformance kit is a test harness and keeps its own catch policy.
    private static readonly string[] s_runtimeLibraries =
    [
        "Dexpace.Sdk.Core",
        "Dexpace.Sdk.Http.SystemNet",
        "Dexpace.Sdk.Serialization.SystemTextJson",
        "Dexpace.Sdk.Extensions.DependencyInjection",
    ];

    [Fact]
    public void No_catch_all_in_a_runtime_library_swallows_a_fatal_exception()
    {
        var violations = RuntimeCatchAlls()
            .Select(site => (Site: site, Reason: FatalFilterScanner.Violation(site)))
            .Where(static found => found.Reason is not null)
            .Select(static found => $"{found.Site.File}:{found.Site.Line}: {found.Reason}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void The_scan_sees_both_kinds_of_catch_all_in_the_real_sources()
    {
        var sites = RuntimeCatchAlls();

        // Not vacuous: filtered and unfiltered catch-alls exist, in more than one library, and the known sites are among them.
        Assert.Contains(sites, static site => site.Filter is null && site.File.EndsWith("Dexpace.Sdk.Core/Http/Request/FileRequestBody.cs", StringComparison.Ordinal));
        Assert.Contains(sites, static site => site.Filter is not null && site.File.EndsWith("Dexpace.Sdk.Core/Internal/Disposal.cs", StringComparison.Ordinal));
        Assert.Contains(sites, static site => site.File.Contains("Dexpace.Sdk.Http.SystemNet/", StringComparison.Ordinal));
        Assert.Contains(sites, static site => site.File.Contains("Dexpace.Sdk.Serialization.SystemTextJson/", StringComparison.Ordinal));
        Assert.Contains(sites, static site => site.File.Contains("Dexpace.Sdk.Extensions.DependencyInjection/", StringComparison.Ordinal));
        Assert.True(sites.Count >= 50, $"the scan found only {sites.Count} catch-alls; the real tree holds well over fifty");
    }

    [Theory]
    [InlineData("try { Work(); } catch (Exception) when (true) { }", 1)]
    [InlineData("try { Work(); } catch (Exception ex) when (ex is Exception) { }", 1)]
    [InlineData("try { Work(); } catch (Exception ex) when (ex is IOException || true) { }", 1)]
    [InlineData("try { Work(); } catch { Log(); }", 1)]
    [InlineData("try { Work(); } catch (Exception ex) { Log(ex); }", 1)]
    [InlineData("try { Work(); } catch (System.Exception ex) { Log(ex); if (again) { throw; } }", 1)]
    [InlineData("try { Work(); } catch (Exception ex) when (!ExceptionFacts.IsFatal(ex)) { Log(ex); }", 0)]
    [InlineData("try { Work(); } catch (Exception ex) when (response is not null && !ExceptionFacts.IsFatal(ex)) { Log(ex); }", 0)]
    [InlineData("try { Work(); } catch (Exception ex) when (EmissionGuard.Reportable(ex, token)) { }", 0)]
    [InlineData("try { Work(); } catch (Exception ex) when (ex is IOException or TimeoutException) { }", 0)]
    [InlineData("try { Work(); } catch { Cleanup(); throw; }", 0)]
    [InlineData("try { Work(); } catch (Exception ex) { Record(ex); throw; }", 0)]
    [InlineData("try { Work(); } catch (Exception ex) { Record(ex); if (ExceptionFacts.IsFatal(ex)) { throw; } }", 0)]
    [InlineData("try { Work(); } catch (IOException) { }", 0)]
    [InlineData("try { Work(); } catch (Exception ex) when (Log(\"}\") && !ExceptionFacts.IsFatal(ex)) { Log(\"{\"); }", 0)]
    [InlineData("// catch { } in a comment, and \"catch (Exception) { }\" in a string, and 'c' in a char", 0)]
    public void The_scanner_flags_a_swallowing_catch_and_passes_a_compliant_one(string source, int violations)
    {
        var found = FatalFilterScanner.CatchAlls("snippet.cs", source)
            .Where(static site => FatalFilterScanner.Violation(site) is not null)
            .ToList();

        Assert.Equal(violations, found.Count);
    }

    [Fact]
    public void The_scanner_reports_the_line_of_the_catch()
    {
        const string Source = "class C\n{\n    void M()\n    {\n        try { }\n        catch (Exception)\n        {\n        }\n    }\n}\n";

        var site = Assert.Single(FatalFilterScanner.CatchAlls("snippet.cs", Source));

        Assert.Equal(6, site.Line);
        Assert.NotNull(FatalFilterScanner.Violation(site));
    }

    private static List<FatalFilterScanner.CatchSite> RuntimeCatchAlls()
    {
        var source = SourceTreeRoot();
        var sites = new List<FatalFilterScanner.CatchSite>();
        foreach (var library in s_runtimeLibraries)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(source, library), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                sites.AddRange(FatalFilterScanner.CatchAlls(Path.GetRelativePath(source, file).Replace('\\', '/'), File.ReadAllText(file)));
            }
        }

        return sites;
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
