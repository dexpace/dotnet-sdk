// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-37, CFG-1, SEAM-6 (P9-5): the registration entry point, one registration per name.</summary>
[Trait("Category", "Unit")]
public sealed class AddDexpaceClientTests
{
    [Fact]
    public void AddDexpaceClient_returns_a_builder_for_the_name()
    {
        var services = new ServiceCollection();

        var builder = services.AddDexpaceClient("github");

        Assert.Equal("github", builder.Name);
        Assert.Same(services, builder.Services);
        Assert.NotNull(builder.HttpClient);
        Assert.Equal("Dexpace.github", builder.HttpClient.Name);
    }

    [Fact]
    public void The_section_overload_takes_a_section_and_rejects_the_root()
    {
        var services = new ServiceCollection();
        var root = Config.Root(("PATH", "/usr/bin"));

        var exception = Assert.Throws<ArgumentException>(() => services.AddDexpaceClient("github", root.GetSection(string.Empty)));

        Assert.Contains("not the configuration root", exception.Message, StringComparison.Ordinal);
        Assert.Equal("github", services.AddDexpaceClient("github", Config.Section()).Name);
    }

    [Fact]
    public void A_second_call_for_the_same_name_returns_the_builder_with_the_same_section_and_throws_otherwise()
    {
        var services = new ServiceCollection();
        var section = Config.Section();
        var first = services.AddDexpaceClient("github", section);

        Assert.Same(first, services.AddDexpaceClient("github", section));
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddDexpaceClient("github", Config.Section()));
        Assert.Contains("'github'", exception.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => services.AddDexpaceClient("github"));

        var bare = new ServiceCollection();
        Assert.Same(bare.AddDexpaceClient("a"), bare.AddDexpaceClient("a"));
    }

    [Fact]
    public void Two_names_are_independent_and_case_sensitive()
    {
        var services = new ServiceCollection();

        var lower = services.AddDexpaceClient("github");
        var upper = services.AddDexpaceClient("GitHub");

        Assert.NotSame(lower, upper);
    }

    [Fact]
    public void Configure_and_ConfigureTransport_record_their_transforms_in_call_order()
    {
        var services = new ServiceCollection();
        var builder = services.AddDexpaceClient("github");
        var registration = (DexpaceClientRegistration)services.Single(d => d.ServiceType == typeof(DexpaceClientRegistration)).KeyedImplementationInstance!;

        Func<Dexpace.Sdk.Core.Configuration.DexpaceClientOptions, Dexpace.Sdk.Core.Configuration.DexpaceClientOptions> first = static o => o;
        Func<Dexpace.Sdk.Core.Configuration.DexpaceClientOptions, Dexpace.Sdk.Core.Configuration.DexpaceClientOptions> second = static o => o;
        Assert.Same(builder, builder.Configure(first).Configure(second));

        Assert.Equal([first, second], registration.OptionsTransforms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddDexpaceClient(name));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddDexpaceClient(null!));
    }
}
