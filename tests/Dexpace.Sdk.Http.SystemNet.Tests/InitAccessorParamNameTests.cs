// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.TestSupport.Source;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// SEAM-29 (phase 11, task 2.5): a null into a validating <c>init</c> accessor of the transport's options names the property, not <c>value</c>. The core and
/// conformance-kit accessors are in <c>InitAccessorParamNameTests</c> of the core tests; this table is the transport's two, and
/// <see cref="The_table_covers_every_validating_accessor_of_the_transport"/> re-scans its sources so a third cannot slip in.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InitAccessorParamNameTests
{
    private static readonly Dictionary<string, Action> s_rows = new(StringComparer.Ordinal)
    {
        ["SystemNetHttpClientOptions.Logger"] = () => _ = new SystemNetHttpClientOptions { Logger = null! },
        ["SystemNetHttpClientOptions.TimeProvider"] = () => _ = new SystemNetHttpClientOptions { TimeProvider = null! },
    };

    public static TheoryData<string> Accessors => new(s_rows.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Accessors))]
    public void A_null_value_names_the_property(string accessor)
    {
        var thrown = Assert.Throws<ArgumentNullException>(s_rows[accessor]);

        Assert.Equal(accessor[(accessor.IndexOf('.', StringComparison.Ordinal) + 1)..], thrown.ParamName);
    }

    [Fact]
    public void The_table_covers_every_validating_accessor_of_the_transport()
    {
        var sites = InitAccessorScanner.Find(SourceDirectory());

        Assert.Equal(sites.Select(static site => site.Key).Order(StringComparer.Ordinal), s_rows.Keys.Order(StringComparer.Ordinal));
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
