// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>
/// XCUT-10 (P9-19, V6; section 11 item 24): one retry layer. Over a loopback server that answers 503, the documented composition (the standard resilience handler
/// with <c>Retry.ShouldHandle</c> returning <see langword="false"/>) sends once per SDK attempt, the standard handler's default arrangement multiplies the
/// attempts and re-sends a POST body behind the SDK's gate, and the handler-side <c>MaxRetryAttempts = 0</c> that the old guidance implied is refused by the handler's own validation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResilienceCompositionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LoopbackServer Unavailable() => LoopbackServer.Start(static _ => LoopbackResponse.Status(503, "Service Unavailable"));

    private static Request Post(Uri url, RequestBody body) => Request.Post(url.AbsoluteUri, body);

    private static ServiceProvider Provider(Action<DexpaceClientBuilder> compose, int attempts)
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(
            services,
            TestClients.Name,
            ("Retry:MaxRetryAttempts", attempts.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Retry:BaseDelay", "1ms"),
            ("Retry:MaxDelay", "1ms"),
            ("Retry:Jitter", "0"));
        compose(builder);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_documented_composition_sends_once_per_SDK_attempt()
    {
        await using var server = Unavailable();
        await using var provider = Provider(static b => b.HttpClient.AddStandardResilienceHandler(static o => o.Retry.ShouldHandle = static _ => ValueTask.FromResult(false)), attempts: 2);
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);

        using var replayable = await pipeline.ExecuteAsync(Post(server.Url("/replay"), RequestBody.FromString("payload")), Ct);

        // MaxRetryAttempts + 1: the SDK is the one retry layer, so the handler adds no re-send.
        Assert.Equal(503, replayable.Status.Code);
        Assert.Equal(3, server.Requests.Count);
    }

    [Fact]
    public async Task A_single_use_body_reaches_the_server_once_behind_the_composition()
    {
        await using var server = Unavailable();
        await using var provider = Provider(static b => b.HttpClient.AddStandardResilienceHandler(static o => o.Retry.ShouldHandle = static _ => ValueTask.FromResult(false)), attempts: 2);
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);
        using var stream = new NonSeekableStream(System.Text.Encoding.UTF8.GetBytes("payload"));

        using var response = await pipeline.ExecuteAsync(Post(server.Url("/once"), RequestBody.FromStream(stream)), Ct);

        Assert.Equal(503, response.Status.Code);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task The_rate_limiter_timeouts_and_circuit_breaker_are_still_in_place()
    {
        await using var provider = Provider(static b => b.HttpClient.AddStandardResilienceHandler(static o => o.Retry.ShouldHandle = static _ => ValueTask.FromResult(false)), attempts: 0);
        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        var options = provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get("Dexpace.github-standard");

        Assert.Equal(TimeSpan.FromSeconds(10), options.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TotalRequestTimeout.Timeout);
        Assert.True(options.CircuitBreaker.FailureRatio > 0);
        Assert.NotNull(options.RateLimiter);
    }

    [Fact]
    public async Task The_standard_handlers_default_arrangement_multiplies_the_attempts_and_resends_a_POST_body()
    {
        // The regression the guidance exists for (XCUT-10(b), section 11 item 24): the SDK is told to make one attempt (MaxRetryAttempts = 0, the option the
        // old guidance offered) and the handler's own default retry still sends the POST body again, behind the SDK's re-send gate.
        await using var server = Unavailable();
        await using var provider = Provider(static b => b.HttpClient.AddStandardResilienceHandler(static o => o.Retry.Delay = TimeSpan.FromMilliseconds(1)), attempts: 0);
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name);

        using var response = await pipeline.ExecuteAsync(Post(server.Url("/multiplied"), RequestBody.FromString("payload")), Ct);

        Assert.True(server.Requests.Count > 1, "the handler's default retry re-sent the body: " + server.Requests.Count);
    }

    [Fact]
    public async Task MaxRetryAttempts_zero_is_refused_by_the_standard_handlers_own_validation()
    {
        await using var provider = Provider(static b => b.HttpClient.AddStandardResilienceHandler(static o => o.Retry.MaxRetryAttempts = 0), attempts: 0);

        var failure = Assert.ThrowsAny<Exception>(() => provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));

        Assert.Contains("MaxRetryAttempts", failure.ToString(), StringComparison.Ordinal);
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
