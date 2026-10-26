// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Source;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// SEAM-29 (phase 11, task 2.5; phase 12, task 1.2): a null into a validating <c>init</c> accessor of the transport's options names the property, not <c>value</c>,
/// and so does a value rejected for its range or format. The core and conformance-kit accessors are in <c>InitAccessorParamNameTests</c> and
/// <c>InitAccessorRejectionParamNameTests</c> of the core tests; these tables are the transport's two null accessors and three rejecting ones, and
/// <see cref="The_table_covers_every_validating_accessor_of_the_transport"/> re-scans its sources so another cannot slip in.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InitAccessorParamNameTests
{
    private static readonly Dictionary<string, Action> s_rows = new(StringComparer.Ordinal)
    {
        ["SystemNetHttpClientOptions.Logger"] = () => _ = new SystemNetHttpClientOptions { Logger = null! },
        ["SystemNetHttpClientOptions.TimeProvider"] = () => _ = new SystemNetHttpClientOptions { TimeProvider = null! },
    };

    // Phase 12, task 1.2 (P12-14): a range or format rejection names the property too; it used to report "value".
    private static readonly Dictionary<string, Action> s_rejectionRows = new(StringComparer.Ordinal)
    {
        ["SystemNetHttpClientOptions.Timeout"] = () => _ = new SystemNetHttpClientOptions { Timeout = TimeSpan.Zero },
        ["SystemNetHttpClientOptions.HeaderDropLogging"] = () => _ = new SystemNetHttpClientOptions { HeaderDropLogging = (HeaderDropLogging)99 },
        ["SystemNetHttpClientOptions.ResendBufferLimit"] = () => _ = new SystemNetHttpClientOptions { ResendBufferLimit = -1 },
    };

    public static TheoryData<string> Accessors => new(s_rows.Keys.Order(StringComparer.Ordinal));

    public static TheoryData<string> Rejections => new(s_rejectionRows.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Accessors))]
    public void A_null_value_names_the_property(string accessor)
    {
        var thrown = Assert.Throws<ArgumentNullException>(s_rows[accessor]);

        Assert.Equal(accessor[(accessor.IndexOf('.', StringComparison.Ordinal) + 1)..], thrown.ParamName);
    }

    [Theory]
    [MemberData(nameof(Rejections))]
    public void A_rejected_value_names_the_property(string accessor)
    {
        var thrown = Assert.ThrowsAny<ArgumentException>(s_rejectionRows[accessor]);

        Assert.Equal(accessor[(accessor.IndexOf('.', StringComparison.Ordinal) + 1)..], thrown.ParamName);
    }

    [Fact]
    public void The_table_covers_every_validating_accessor_of_the_transport()
    {
        var sites = InitAccessorScanner.Find(SourceDirectory());

        Assert.Equal(
            sites.Where(static site => site.Kind == InitAccessorScanner.SiteKind.Null).Select(static site => site.Key).Order(StringComparer.Ordinal),
            s_rows.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            sites.Where(static site => site.Kind == InitAccessorScanner.SiteKind.Rejection).Select(static site => site.Key).Order(StringComparer.Ordinal),
            s_rejectionRows.Keys.Order(StringComparer.Ordinal));
        Assert.All(sites, static site => Assert.True(site.NamesItsProperty, $"{site.File}:{site.Line} ({site.Key})"));
    }

    private static string SourceDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Dexpace.Sdk.Http.SystemNet");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The src/Dexpace.Sdk.Http.SystemNet tree was not found above the test output.");
    }
}
