// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>XCUT-18, XCUT-19 (P9-18): no failure message, no <c>ToString</c> and no log line from a bad <c>Auth:*</c> or proxy configuration contains a configured secret.</summary>
[Trait("Category", "Unit")]
public sealed class CredentialRedactionTests
{
    private static readonly string[] s_secrets = ["s3cr3t-key", "s3cr3t-pass", "s3cr3t-digest", "s3cr3t-proxy"];

    [Fact]
    public async Task No_secret_appears_in_any_failure_string_or_log_line_of_a_bad_configuration()
    {
        using var recorder = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(recorder));
        _ = TestClients.Add(
            services,
            TestClients.Name,
            ("Auth:ApiKey:Key", "s3cr3t-key"),
            ("Auth:Basic:Username", "alice"),
            ("Auth:Basic:Password", "s3cr3t-pass"),
            ("Auth:Digest:Username", "bob"),
            ("Auth:Digest:Password", "s3cr3t-digest"),
            ("Transport:Proxy:Host", "proxy.test"),
            ("Transport:Proxy:Port", "not-a-port"),
            ("Transport:Proxy:Password", "s3cr3t-proxy"),
            ("Retry:MaxRetryAttempts", "abc"));
        await using var provider = services.BuildServiceProvider();

        var startup = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);
        var resolution = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));
        var state = ConfigurationReader.Read(Config.Section(
            ("Auth:ApiKey:Key", "s3cr3t-key"),
            ("Auth:Basic:Username", "alice"),
            ("Auth:Basic:Password", "s3cr3t-pass"),
            ("Auth:Digest:Username", "bob"),
            ("Auth:Digest:Password", "s3cr3t-digest")));

        var everything = string.Join(
            '\n',
            startup.Message,
            startup.ToString(),
            resolution.Message,
            resolution.ToString(),
            state.Credentials!.ToString(),
            string.Join('\n', recorder.Entries.Select(static e => e.Message)),
            string.Join('\n', Config.Failures(state)));
        foreach (var secret in s_secrets)
        {
            Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        }

        Assert.Contains("Transport:Proxy:Port", startup.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_first_call_logs_no_secret()
    {
        using var recorder = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(recorder));
        var scripted = new Dexpace.Sdk.TestSupport.Transports.ScriptedTransport(new InvalidOperationException("boom"));
        _ = TestClients.Add(services, TestClients.Name, ("Auth:ApiKey:Key", "s3cr3t-key"), ("Retry:MaxRetryAttempts", "0"))
            .UseTransport(scripted)
            .UseAuth(new Dexpace.Sdk.Core.Auth.AuthDescriptor(new Dexpace.Sdk.Core.Auth.AuthRequirement(Dexpace.Sdk.Core.Auth.AuthScheme.ApiKey)));
        await using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<Dexpace.Sdk.Core.Pipeline.HttpPipeline>(TestClients.Name);

        await Assert.ThrowsAnyAsync<Exception>(async () => await pipeline.ExecuteAsync(Request.Get("https://api.test/x"), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(recorder.Entries, static e => e.Message.Contains("s3cr3t-key", StringComparison.Ordinal));
    }
}
