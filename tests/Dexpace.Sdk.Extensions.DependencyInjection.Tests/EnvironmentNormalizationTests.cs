// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>
/// CFG-3, CFG-4, CFG-11 (section 10 entry 25): .NET's own rule maps <c>__</c> to <c>:</c> case-insensitively, and the reader sits on it. The only
/// suite that reads the real process environment, under a per-test GUID prefix, removed in <c>finally</c> (CFG-11: no other test does).
/// </summary>
[Trait("Category", "Integration")]
[Collection("ProcessEnvironment")]
public sealed class EnvironmentNormalizationTests
{
    private static IConfigurationSection SectionFor(string prefix) =>
        new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build().GetSection(string.Empty);

    [Fact]
    public void A_double_underscore_variable_resolves_under_the_colon_key()
    {
        var prefix = NewPrefix();
        var name = prefix + "Retry__MaxRetryAttempts";
        Environment.SetEnvironmentVariable(name, "4");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();

            Assert.Equal("4", configuration["Retry:MaxRetryAttempts"]);
            var read = ConfigurationReader.Read(Nested(configuration));
            Assert.Empty(read.Failures);
            Assert.Equal(4, read.Options.Retry.MaxRetryAttempts);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        Assert.Null(Environment.GetEnvironmentVariable(name));
    }

    [Fact]
    public void A_camel_case_and_an_upper_case_spelling_resolve_alike()
    {
        // CFG-4 (N/A, vacuous): nothing normalises casing away, so there is no raw accessor to provide.
        var prefix = NewPrefix();
        var upper = prefix + "RETRY__MAXRETRYATTEMPTS";
        var lower = NewPrefix() + "retry__maxretryattempts";
        Environment.SetEnvironmentVariable(upper, "5");
        Environment.SetEnvironmentVariable(lower, "6");
        try
        {
            var first = ConfigurationReader.Read(Nested(new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build()));
            var second = ConfigurationReader.Read(Nested(new ConfigurationBuilder().AddEnvironmentVariables(lower[..lower.IndexOf("retry", StringComparison.Ordinal)]).Build()));

            Assert.Equal(5, first.Options.Retry.MaxRetryAttempts);
            Assert.Equal(6, second.Options.Retry.MaxRetryAttempts);
        }
        finally
        {
            Environment.SetEnvironmentVariable(upper, null);
            Environment.SetEnvironmentVariable(lower, null);
        }
    }

    [Fact]
    public void A_variable_outside_the_prefix_is_never_read()
    {
        var mine = NewPrefix();
        var other = NewPrefix() + "Retry__MaxRetryAttempts";
        Environment.SetEnvironmentVariable(other, "9");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(mine).Build();

            Assert.Null(configuration["Retry:MaxRetryAttempts"]);
            Assert.Empty(ConfigurationReader.Read(Nested(configuration)).Failures);
        }
        finally
        {
            Environment.SetEnvironmentVariable(other, null);
        }
    }

    [Fact]
    public void The_production_default_delegates_to_the_environment_and_leaves_no_residue()
    {
        // CFG-11: AddEnvironmentVariables is what an unconfigured host reads; a variable removed afterwards is gone.
        var prefix = NewPrefix();
        var name = prefix + "UserAgent";
        Environment.SetEnvironmentVariable(name, "from-env");
        var seen = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build()["UserAgent"];
        Environment.SetEnvironmentVariable(name, null);
        var after = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build()["UserAgent"];

        Assert.Equal("from-env", seen);
        Assert.Null(after);
        _ = SectionFor(prefix);
    }

    // The reader needs a section with a non-empty path; the environment provider's keys sit at the root, so nest them under one.
    private static IConfigurationSection Nested(IConfigurationRoot environment)
    {
        var values = environment.AsEnumerable().Where(static p => p.Value is not null).Select(static p => ($"{Config.SectionPath}:{p.Key}", p.Value));
        return Config.Root([.. values]).GetSection(Config.SectionPath);
    }

    private static string NewPrefix() => "DEXPACE_TEST_" + Guid.NewGuid().ToString("N") + "__";
}
