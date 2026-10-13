// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.AotSmoke;

// Phase 9: the DI package. The configuration reader is plain code (no binder, no reflection), the factories are lambdas and the
// enum names come from Enum.GetNames<T>(), so registration, validation and the named client's transport must survive trimming and
// NativeAOT (P9-21, V7). The client registers a stub codec because a client without one fails validation (design D).
internal static partial class SmokeChecks
{
    private static async Task CheckPhase9Async()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smoke:Retry:BaseDelay"] = "250ms",
                ["Smoke:Logging:Level"] = "Headers",
                ["Smoke:Redirect:AllowedMethods"] = "GET, HEAD",
                ["Smoke:Retry:RetryableStatusCodes"] = "429, 503",
            })
            .Build();
        var services = new ServiceCollection();
        _ = services.AddDexpaceClient("smoke", configuration.GetSection("Smoke")).UseSerde(new StubSerde());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        provider.GetRequiredService<IStartupValidator>().Validate();
        var options = provider.GetRequiredKeyedService<DexpaceClientOptions>("smoke");
        Expect(options.Retry.BaseDelay == TimeSpan.FromMilliseconds(250), "The DI reader parses a shorthand duration");
        Expect(options.Logging.Level == HttpLogLevel.Headers, "The DI reader parses a log level by name");
        Expect(options.Redirect.AllowedMethods.Count == 2 && options.Redirect.AllowedMethods.Contains(Method.Head), "The DI reader parses a method list");
        Expect(options.Retry.RetryableStatusCodes.Count == 2 && options.Retry.RetryableStatusCodes.Contains(503), "The DI reader parses a status list");
        Expect(provider.GetRequiredKeyedService<HttpPipeline>("smoke") is not null, "The keyed pipeline resolves");

        await using var server = LoopbackServer.Start(LoopbackResponse.Redirect(302, "http://127.0.0.1:9/never"));
        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>("smoke");
        using var response = await transport.ExecuteAsync(Request.Get(server.Url("/start").AbsoluteUri), CancellationToken.None);
        Expect(response.Status.Code == 302, "The named client's transport returns the 302 unchanged (TRANSPORT-1)");

        var bad = new ServiceCollection();
        _ = bad.AddDexpaceClient("bad", new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Bad:Retry:MaxRetryAttempts"] = "abc" }).Build().GetSection("Bad"))
            .UseSerde(new StubSerde());
        await using var badProvider = bad.BuildServiceProvider();
        var failure = Record(() => badProvider.GetRequiredService<IStartupValidator>().Validate());
        Expect(failure is not null, "A bad value fails validation with OptionsValidationException");
        Expect(failure!.Message.Contains("Retry:MaxRetryAttempts", StringComparison.Ordinal) && !failure.Message.Contains("abc", StringComparison.Ordinal), "The failure names the key and never the value");
    }

    private static OptionsValidationException? Record(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (OptionsValidationException ex)
        {
            return ex;
        }
    }

    // A codec that does nothing: the check is about registration, not about wire formats.
    private sealed class StubSerde : ISerde
    {
        public MediaType DefaultMediaType => CommonMediaTypes.ApplicationJson;

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default) => ValueTask.FromResult<T?>(default);

        public void Serialize<T>(IBufferWriter<byte> destination, T value)
        {
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => default;
    }
}
