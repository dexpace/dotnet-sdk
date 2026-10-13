// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>TRANSPORT-1, OBS-11 (P9-12, P9-13, P9-14; V3, V9): the named client's handler chain, its primary and what the package takes out of the factory's defaults.</summary>
[Trait("Category", "Unit")]
public sealed class NamedClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (ServiceCollection Services, DexpaceClientBuilder Builder, ChainSpy Spy) Client(params (string, string?)[] values)
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services, TestClients.Name, values);
        var spy = new ChainSpy();
        builder.HttpClient.AddHttpMessageHandler(() => spy);
        return (services, builder, spy);
    }

    [Fact]
    public async Task The_keyed_transport_is_a_SystemNetHttpClient_over_a_redirect_off_primary()
    {
        var (services, _, spy) = Client();
        await using var provider = services.BuildServiceProvider();

        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        Assert.IsType<SystemNetHttpClient>(transport);
        var primary = Assert.IsType<SocketsHttpHandler>(spy.Primary);
        Assert.False(primary.AllowAutoRedirect);
        Assert.False(primary.UseProxy);
    }

    [Fact]
    public async Task The_package_removes_the_factorys_loggers_so_the_SDKs_events_are_the_one_HTTP_log_stream()
    {
        using var recorder = new RecordingLoggerProvider();
        var (services, _, _) = Client();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(recorder));
        await using var provider = services.BuildServiceProvider();
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("hi"));
        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/secret-path/token123?x=1").AbsoluteUri), Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.DoesNotContain(recorder.Entries, e => e.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal));
        Assert.DoesNotContain(recorder.Entries, e => e.Message.Contains("token123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_handler_lifetime_is_infinite()
    {
        var (services, _, _) = Client();
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get("Dexpace.github");

        Assert.Equal(Timeout.InfiniteTimeSpan, options.HandlerLifetime);
    }

    [Theory]
    [InlineData(null, 120)]
    [InlineData("10s", 10)]
    [InlineData("none", -1)]
    public async Task The_pooled_connection_lifetime_is_the_configured_value_and_two_minutes_by_default(string? configured, int seconds)
    {
        var (services, _, spy) = configured is null ? Client() : Client(("Transport:PooledConnectionLifetime", configured));
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        var primary = Assert.IsType<SocketsHttpHandler>(spy.Primary);
        Assert.Equal(seconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds), primary.PooledConnectionLifetime);
    }

    [Fact]
    public async Task ConfigureHttpClient_is_ignored_because_the_package_uses_the_handler_and_never_CreateClient()
    {
        var (services, builder, spy) = Client();
        builder.HttpClient.ConfigureHttpClient(c =>
        {
            c.BaseAddress = new Uri("http://ignored.invalid/");
            c.DefaultRequestHeaders.Add("X-Extra", "should-not-be-sent");
        });
        services.AddSingleton<IHttpClientFactory>(_ => throw new InvalidOperationException("CreateClient must never be the package's route"));
        await using var provider = services.BuildServiceProvider();
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("hi"));
        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/x").AbsoluteUri), Ct);

        Assert.Equal(200, response.Status.Code);
        Assert.Empty(Assert.Single(server.Requests).HeaderValues("x-extra"));
        Assert.Single(spy.Seen);
    }

    [Fact]
    public async Task A_substituted_redirect_following_primary_is_refused_at_first_resolution()
    {
        var (services, builder, _) = Client();
        builder.HttpClient.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler());
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<ArgumentException>(() => provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));

        Assert.Contains("AllowAutoRedirect", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_client_with_a_bad_configuration_fails_the_resolution_of_its_transport()
    {
        var (services, _, _) = Client(("Retry:MaxRetryAttempts", "abc"));
        await using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));
    }
}
