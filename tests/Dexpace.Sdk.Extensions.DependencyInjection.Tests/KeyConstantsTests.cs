// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-14 (P9-23): the published keys are exact, name what <c>ProxyOptions.FromEnvironment</c> reads, and are never read by the package.</summary>
[Trait("Category", "Unit")]
public sealed class KeyConstantsTests
{
    [Fact]
    public void The_published_values_are_exact_and_the_set_is_closed()
    {
        Assert.Equal("Dexpace", DexpaceConfigurationKeys.SectionName);
        Assert.Equal("Retry:MaxRetryAttempts", DexpaceConfigurationKeys.MaxRetryAttempts);
        Assert.Equal("Logging:Level", DexpaceConfigurationKeys.LogLevel);
        Assert.Equal("HTTP_PROXY", DexpaceConfigurationKeys.HttpProxy);
        Assert.Equal("HTTPS_PROXY", DexpaceConfigurationKeys.HttpsProxy);
        Assert.Equal("NO_PROXY", DexpaceConfigurationKeys.NoProxy);

        var constants = typeof(DexpaceConfigurationKeys).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(6, constants.Length);
        Assert.All(constants, static f => Assert.True(f.IsLiteral));
    }

    [Fact]
    public void The_proxy_names_are_what_ProxyOptions_FromEnvironment_reads()
    {
        var asked = new List<string>();
        _ = ProxyOptions.FromEnvironment(
            key =>
            {
                asked.Add(key);
                return null;
            },
            NullLogger.Instance);

        // NO_PROXY is consulted only once a proxy was found.
        _ = ProxyOptions.FromEnvironment(
            key =>
            {
                asked.Add(key);
                return key == "HTTPS_PROXY" ? "http://proxy.test:3128" : null;
            },
            NullLogger.Instance);

        Assert.Contains(DexpaceConfigurationKeys.HttpProxy, asked);
        Assert.Contains(DexpaceConfigurationKeys.HttpsProxy, asked);
        Assert.Contains(DexpaceConfigurationKeys.NoProxy, asked);
    }

    [Fact]
    public void The_section_name_is_not_a_key_the_key_table_expects()
    {
        // Published, not read: the table's keys are all relative to the caller's section, and none is the conventional section name.
        Assert.DoesNotContain(KeyTable.Entries, static e => string.Equals(e.Key, DexpaceConfigurationKeys.SectionName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_member_of_the_package_reads_SectionName()
    {
        // A const is inlined, so the scan is over the string literals loaded in the package: the section name is published, never read, and no key is
        // baked in (OBS-35). The literal appears nowhere in the package's own code.
        var offenders = Architecture.IlCallScanner.FindStrings(typeof(DexpaceClientBuilder).Assembly)
            .Where(static s => string.Equals(s.Value, DexpaceConfigurationKeys.SectionName, StringComparison.Ordinal))
            .Select(static s => s.CallerMethod.DeclaringType?.Name + "." + s.CallerMethod.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scanner_finds_a_planted_ldstr()
    {
        var planted = Architecture.IlCallScanner.FindStrings(typeof(KeyConstantsTests).Assembly)
            .Where(static s => s.CallerMethod.DeclaringType == typeof(Planted) && s.Value == "Dexpace");

        Assert.NotEmpty(planted);
    }

    private static class Planted
    {
        internal static string Reads() => "Dexpace";
    }
}
