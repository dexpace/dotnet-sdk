// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>The roadmap card's provider test (styleguide 2.5; SEAM-5, NFR-8; V4): <c>ValidateScopes</c> and <c>ValidateOnBuild</c> over every keyed registration, root and scope.</summary>
[Trait("Category", "Unit")]
public sealed class ProviderValidationTests
{
    private static readonly ServiceProviderOptions s_strict = new() { ValidateScopes = true, ValidateOnBuild = true };

    [Fact]
    public async Task Every_keyed_registration_resolves_from_the_root_and_from_a_scope()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services, "first");
        _ = TestClients.Add(services, "second").UseTransport(new ScriptedTransport());
        await using var provider = services.BuildServiceProvider(s_strict);
        await using var scope = provider.CreateAsyncScope();

        foreach (var name in new[] { "first", "second" })
        {
            foreach (var source in new IServiceProvider[] { provider, scope.ServiceProvider })
            {
                var keyed = (IKeyedServiceProvider)source;
                Assert.NotNull(keyed.GetRequiredKeyedService<HttpPipeline>(name));
                Assert.NotNull(keyed.GetRequiredKeyedService<IAsyncHttpClient>(name));
                Assert.NotNull(keyed.GetRequiredKeyedService<ISerde>(name));
                Assert.NotNull(keyed.GetRequiredKeyedService<DexpaceClientOptions>(name));
            }
        }
    }

    [Fact]
    public async Task A_factory_that_captures_a_scoped_service_fails_under_ValidateScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<NamedPipelineTests.ScopedThing>();
        _ = services.AddDexpaceClient(TestClients.Name, Config.Section()).UseSerde(sp => new CapturingSerde(sp.GetRequiredService<NamedPipelineTests.ScopedThing>()));
        await using var provider = services.BuildServiceProvider(s_strict);

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredKeyedService<ISerde>(TestClients.Name));
    }

    [Fact]
    public async Task ValidateOnBuild_does_not_run_options_validation_so_the_failure_arrives_at_start_or_first_resolution()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services, TestClients.Name, ("Retry:MaxRetryAttempts", "abc"));
        await using var provider = services.BuildServiceProvider(s_strict);

        // V4: building the provider did not throw; the validator is what fails.
        Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);
    }

    private sealed class CapturingSerde(NamedPipelineTests.ScopedThing thing) : ISerde
    {
        public NamedPipelineTests.ScopedThing Thing { get; } = thing;

        public Dexpace.Sdk.Core.Http.Common.MediaType DefaultMediaType => Dexpace.Sdk.Core.Http.Common.CommonMediaTypes.ApplicationJson;

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default) => ValueTask.FromResult<T?>(default);

        public void Serialize<T>(System.Buffers.IBufferWriter<byte> destination, T value)
        {
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => default;
    }
}
