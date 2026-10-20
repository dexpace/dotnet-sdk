// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>
/// CFG-2 (design §10 entry 25, §11 item 68, P11-17): an empty or whitespace-only value is absent, and when the registered configuration is the root that
/// holds the caller's section it falls through to the next provider that has a value. A section the registered configuration does not hold keeps the merged
/// read, where an empty value yields the record default.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EmptyValueTests
{
    private const string SectionPath = "Dexpace:c";

    private static IConfigurationRoot Layers(params (string Key, string? Value)[][] layers)
    {
        var builder = new ConfigurationBuilder();
        foreach (var layer in layers)
        {
            builder.AddInMemoryCollection(layer.Select(static v => new KeyValuePair<string, string?>(SectionPath + ":" + v.Key, v.Value)));
        }

        return builder.Build();
    }

    // The public registration path: the configuration is registered the way a host registers it, and the client's section is a slice of it.
    private static DexpaceClientState Resolve(IConfiguration? registered, IConfigurationSection section)
    {
        var services = new ServiceCollection();
        if (registered is not null)
        {
            services.AddSingleton(registered);
        }

        services.AddDexpaceClient("c", section).UseSerde(StubSerde.Instance);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<DexpaceClientState>>().Get("c");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void An_empty_value_falls_through_to_the_lower_provider(string empty)
    {
        var root = Layers([("Retry:MaxRetryAttempts", "5")], [("Retry:MaxRetryAttempts", empty)]);

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Empty(state.Failures);
        Assert.Equal(5, state.Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void A_non_empty_value_in_a_higher_provider_still_wins()
    {
        var root = Layers([("Retry:MaxRetryAttempts", "5")], [("Retry:MaxRetryAttempts", "7")], [("Retry:BaseDelay", "")]);

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Equal(7, state.Options.Retry.MaxRetryAttempts);
        Assert.Equal(new RetryOptions().BaseDelay, state.Options.Retry.BaseDelay);
    }

    [Fact]
    public void An_empty_value_with_nothing_below_it_is_absent_and_the_record_default_applies()
    {
        var root = Layers([("UserAgent", "")], [("UserAgent", "   ")], [("Retry:MaxRetryAttempts", "")]);

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Empty(state.Failures);
        var defaults = new DexpaceClientOptions();
        Assert.Equal(defaults.UserAgent, state.Options.UserAgent);
        Assert.Equal(defaults.Retry, state.Options.Retry);
    }

    [Fact]
    public void Every_kind_of_key_falls_through()
    {
        var lower = new[]
        {
            ("UserAgent", "lower-agent"),
            ("Retry:MaxRetryAttempts", "7"),
            ("Retry:BaseDelay", "9s"),
            ("Retry:RetryableStatusCodes", "500, 502"),
            ("Redirect:FollowSeeOther", "true"),
        };
        var higher = new[]
        {
            ("UserAgent", ""),
            ("Retry:MaxRetryAttempts", ""),
            ("Retry:BaseDelay", " "),
            ("Retry:RetryableStatusCodes", ""),
            ("Redirect:FollowSeeOther", "\t"),
        };
        var root = Layers([.. lower], [.. higher]);

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Empty(state.Failures);
        Assert.Equal("lower-agent", state.Options.UserAgent);
        Assert.Equal(7, state.Options.Retry.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(9), state.Options.Retry.BaseDelay);
        Assert.True(state.Options.Retry.RetryableStatusCodes.SetEquals([500, 502]));
        Assert.True(state.Options.Redirect.FollowSeeOther);
    }

    [Fact]
    public void An_empty_list_item_falls_through_to_the_lower_items_value()
    {
        var root = Layers(
            [("Retry:RetryableStatusCodes:0", "503"), ("Retry:RetryableStatusCodes:1", "504")],
            [("Retry:RetryableStatusCodes:0", ""), ("Retry:RetryableStatusCodes:1", "   ")]);

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Empty(state.Failures);
        Assert.True(state.Options.Retry.RetryableStatusCodes.SetEquals([503, 504]));
    }

    [Fact]
    public void A_section_from_an_unregistered_configuration_keeps_the_merged_read()
    {
        // No IConfiguration in the container, so the reader cannot know which providers sit under the section: an empty value is absent and yields the default.
        var root = Layers([("Retry:MaxRetryAttempts", "5")], [("Retry:MaxRetryAttempts", "")]);
        var other = Layers([("Retry:MaxRetryAttempts", "9")]);

        var unregistered = Resolve(null, root.GetSection(SectionPath));
        var differentRoot = Resolve(other, root.GetSection(SectionPath));

        Assert.Empty(unregistered.Failures);
        Assert.Equal(new RetryOptions().MaxRetryAttempts, unregistered.Options.Retry.MaxRetryAttempts);
        Assert.Equal(new RetryOptions().MaxRetryAttempts, differentRoot.Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void A_registered_configuration_that_is_not_a_root_keeps_the_merged_read()
    {
        var root = Layers([("Retry:MaxRetryAttempts", "5")], [("Retry:MaxRetryAttempts", "")]);
        var section = root.GetSection(SectionPath);

        var state = Resolve(section, section);

        Assert.Equal(new RetryOptions().MaxRetryAttempts, state.Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void A_chained_configuration_is_walked()
    {
        var lower = Layers([("Retry:MaxRetryAttempts", "5"), ("UserAgent", "chained-agent")]);
        var root = new ConfigurationBuilder()
            .AddConfiguration(lower)
            .AddInMemoryCollection(new Dictionary<string, string?> { [SectionPath + ":Retry:MaxRetryAttempts"] = "", [SectionPath + ":UserAgent"] = " " })
            .Build();

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Empty(state.Failures);
        Assert.Equal(5, state.Options.Retry.MaxRetryAttempts);
        Assert.Equal("chained-agent", state.Options.UserAgent);
    }

    [Fact]
    public void A_chained_configuration_with_its_own_empty_value_falls_through_inside_the_chain()
    {
        var inner = Layers([("Retry:MaxRetryAttempts", "5")], [("Retry:MaxRetryAttempts", "")]);
        var root = new ConfigurationBuilder().AddConfiguration(inner).Build();

        var state = Resolve(root, root.GetSection(SectionPath));

        Assert.Equal(5, state.Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void A_ConfigurationManager_registered_as_the_configuration_is_walked()
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(new Dictionary<string, string?> { [SectionPath + ":Retry:MaxRetryAttempts"] = "5" });
        manager.AddInMemoryCollection(new Dictionary<string, string?> { [SectionPath + ":Retry:MaxRetryAttempts"] = "" });

        var state = Resolve(manager, manager.GetSection(SectionPath));

        Assert.Equal(5, state.Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void Every_kind_of_key_treats_empty_as_absent()
    {
        // The reader on its own, over a section with no registered root: the merged read, where an empty value yields the record default.
        var higher = new[]
        {
            ("UserAgent", ""),
            ("Retry:MaxRetryAttempts", ""),
            ("Retry:BaseDelay", " "),
            ("Retry:RetryableStatusCodes", ""),
        };
        var lower = new[]
        {
            ("UserAgent", "lower-agent"),
            ("Retry:MaxRetryAttempts", "7"),
            ("Retry:BaseDelay", "9s"),
            ("Retry:RetryableStatusCodes", "500"),
        };

        var configuration = ConfigurationReader.Read(Config.Layered([.. lower], [.. higher]));

        Assert.Empty(configuration.Failures);
        var defaults = new DexpaceClientOptions();
        Assert.Equal(defaults.UserAgent, configuration.Options.UserAgent);
        Assert.NotEqual(string.Empty, configuration.Options.UserAgent);
        Assert.Equal(defaults.Retry, configuration.Options.Retry);
    }
}
