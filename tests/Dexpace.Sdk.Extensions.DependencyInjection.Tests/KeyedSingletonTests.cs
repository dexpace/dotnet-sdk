// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>
/// SEAM-7 (the singleton lifetime of a built container): 64 concurrent first resolutions run each factory once. "An unresolved state stays
/// re-evaluable" has nothing to apply to, because a built container gains no registrations; SEAM-7 stays N/A with this test cited.
/// </summary>
[Trait("Category", "Unit")]
public sealed class KeyedSingletonTests
{
    private const int Threads = 64;

    [Fact]
    public async Task Sixty_four_concurrent_first_resolutions_run_each_factory_once_and_share_one_instance()
    {
        var transportBuilds = 0;
        var serdeBuilds = 0;
        var services = new ServiceCollection();
        _ = services.AddDexpaceClient(TestClients.Name, Config.Section())
            .UseTransport(_ =>
            {
                Interlocked.Increment(ref transportBuilds);
                return new ScriptedTransport();
            })
            .UseSerde(_ =>
            {
                Interlocked.Increment(ref serdeBuilds);
                return StubSerde.Instance;
            });
        await using var provider = services.BuildServiceProvider();

        var transports = await Race(() => provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));
        var serdes = await Race(() => provider.GetRequiredKeyedService<ISerde>(TestClients.Name));
        var pipelines = await Race(() => provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name));
        var options = await Race(() => provider.GetRequiredKeyedService<DexpaceClientOptions>(TestClients.Name));

        Assert.Equal(1, transportBuilds);
        Assert.Equal(1, serdeBuilds);
        Assert.Single(transports.Distinct());
        Assert.Single(serdes.Distinct());
        Assert.Single(pipelines.Distinct());
        Assert.Single(options.Distinct());
    }

    // Dedicated threads released together: a pool-starving barrier would deadlock (the 8b finding).
    private static async Task<List<T>> Race<T>(Func<T> resolve)
        where T : class
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, Threads)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.Wait();
                    return resolve();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToList();
        start.Set();
        return [.. await Task.WhenAll(tasks)];
    }
}
