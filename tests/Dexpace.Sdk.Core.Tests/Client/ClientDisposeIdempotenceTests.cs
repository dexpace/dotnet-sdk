// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Threading;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// SEAM-14, the adapters' half (closed by phase 3b): <see cref="DelegateHttpClient"/>'s and both bridges' disposes hold
/// nothing, so repeating them is idempotent trivially. All pins.
/// </summary>
[Trait("Category", "Unit")]
public class ClientDisposeIdempotenceTests
{
    private static Request NewRequest() => Request.Get("https://api.example.com/v1/items");

    [Fact]
    public async Task Bridge_and_delegate_disposes_are_idempotent()
    {
        var asyncDelegate = DelegateHttpClient.Create((r, _, _) => Task.FromResult(TestResponses.Create(Status.Ok, r)));
        await asyncDelegate.DisposeAsync();
        await asyncDelegate.DisposeAsync();

        var syncDelegate = DelegateHttpClient.CreateBlocking((r, _, _) => TestResponses.Create(Status.Ok, r));
        syncDelegate.Dispose();
        syncDelegate.Dispose();

        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport();
        var bridge = transport.AsAsync(scheduler);
        await bridge.DisposeAsync();
        await bridge.DisposeAsync();

        var blocking = bridge.AsBlocking();
        blocking.Dispose();
        blocking.Dispose();

        // The adapters hold nothing, so the wrapped client keeps working after any of the above.
        using var response = await asyncDelegate.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccess);
    }
}
