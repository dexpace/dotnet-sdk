// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Common = Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>PIPE-27, PIPE-35, CFG-8 (P9-5; V3): the keyed pipeline is the default policies over the keyed transport with the bound options.</summary>
[Trait("Category", "Unit")]
public sealed class NamedPipelineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_keyed_pipeline_runs_the_default_policies_with_the_bound_options()
    {
        var scripted = new ScriptedTransport(
            TestResponses.Create(Status.FromCode(503)),
            TestResponses.Create(Status.FromCode(200)));
        var services = new ServiceCollection();
        _ = TestClients.Add(
                services,
                TestClients.Name,
                ("Retry:MaxRetryAttempts", "1"),
                ("Retry:BaseDelay", "1ms"),
                ("Retry:Jitter", "0"))
            .UseTransport(scripted);
        await using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);

        using var response = await pipeline.ExecuteAsync(Request.Get("https://api.test/x"), Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, scripted.CallCount);
    }

    [Fact]
    public async Task AddPolicy_places_a_policy_by_stage_and_keeps_call_order_within_a_stage()
    {
        var log = new List<string>();
        var scripted = new ScriptedTransport(TestResponses.Create(Status.FromCode(200)));
        var services = new ServiceCollection();
        _ = TestClients.Add(services)
            .UseTransport(scripted)
            .AddPolicy(_ => new ProbePolicy("attempt", PipelineStage.PerAttempt, log))
            .AddPolicy(_ => new ProbePolicy("call-1", PipelineStage.PerCall, log))
            .AddPolicy(_ => new ProbePolicy("call-2", PipelineStage.PerCall, log));
        await using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);

        using var response = await pipeline.ExecuteAsync(Request.Get("https://api.test/x"), Ct);

        int At(string name) => log.FindIndex(e => e.Contains(name, StringComparison.Ordinal));
        Assert.True(At("call-1") >= 0 && At("call-1") < At("call-2"));
        Assert.True(At("call-2") < At("attempt"));
    }

    [Fact]
    public async Task The_keyed_options_is_the_snapshot_the_pipeline_was_built_with_and_stays_immutable()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services, TestClients.Name, ("UserAgent", "acme/1"), ("Redirect:MaxRedirects", "5")).UseTransport(new ScriptedTransport());
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredKeyedService<DexpaceClientOptions>(TestClients.Name);
        _ = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);

        Assert.Equal("acme/1", options.UserAgent);
        var changed = options with { UserAgent = "other" };
        Assert.Equal("acme/1", provider.GetRequiredKeyedService<DexpaceClientOptions>(TestClients.Name).UserAgent);
        Assert.Equal("other", changed.UserAgent);
    }

    [Fact]
    public async Task The_pipeline_does_not_own_the_transport()
    {
        using var scripted = new ScriptedTransport();
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(_ => scripted);
        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);

        await pipeline.DisposeAsync();
        Assert.False(scripted.IsDisposed);

        await provider.DisposeAsync();
        Assert.True(scripted.IsDisposed);
    }

    [Fact]
    public async Task A_factory_cannot_take_a_scoped_service_because_it_runs_on_the_root_provider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedThing>();
        _ = services.AddDexpaceClient(TestClients.Name, Config.Section())
            .UseSerde(sp => new ScopedSerde(sp.GetRequiredService<ScopedThing>()));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredKeyedService<Dexpace.Sdk.Core.Serialization.ISerde>(TestClients.Name));
    }

    internal sealed class ScopedThing;

    private sealed class ScopedSerde(ScopedThing thing) : Dexpace.Sdk.Core.Serialization.ISerde
    {
        public ScopedThing Thing { get; } = thing;

        public Common.MediaType DefaultMediaType => Common.CommonMediaTypes.ApplicationJson;

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default) => ValueTask.FromResult<T?>(default);

        public void Serialize<T>(System.Buffers.IBufferWriter<byte> destination, T value)
        {
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => default;
    }
}
