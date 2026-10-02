// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Threading;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// SEAM-12 (design §3.2, §1): core's own implementations keep all per-call state in locals, so concurrent calls never
/// cross-talk. The per-transport proof is phase 8a's conformance kit.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConcurrencyTests
{
    private const int Calls = 64;

    private static Response Echo(Request request) =>
        TestResponses.Create(
            Status.Ok,
            request,
            new Headers.Builder().Set("X-Echo-Path", request.Url.AbsolutePath).Build());

    private static IEnumerable<(Request Request, RequestOptions Options)> Inputs() =>
        Enumerable.Range(0, Calls).Select(i => (
            Request.Get($"https://api.example.com/v1/items/{i}"),
            new RequestOptions { MaxRetries = i }));

    private static void AssertPaired(IEnumerable<(Request Request, RequestOptions Options)> inputs, Response[] responses, IReadOnlyList<RecordedCall> calls)
    {
        var inputList = inputs.ToList();
        Assert.Equal(Calls, calls.Count);
        for (var i = 0; i < Calls; i++)
        {
            Assert.Equal(inputList[i].Request.Url.AbsolutePath, responses[i].Headers.Get("X-Echo-Path"));
            Assert.Same(inputList[i].Request, responses[i].Request);
            var call = Assert.Single(calls, c => ReferenceEquals(c.Request, inputList[i].Request));
            Assert.Same(inputList[i].Options, call.Options);
        }
    }

    [Fact]
    public async Task No_cross_talk_through_the_bridges()
    {
        var inputs = Inputs().ToList();

        // AsAsync over a scheduler that gives every call its own thread.
        using var scheduler = new RecordingTaskScheduler();
        using var syncTransport = new RecordingSyncTransport(Echo);
        await using var asAsync = syncTransport.AsAsync(scheduler);
        var asyncTasks = inputs.Select(i => asAsync.ExecuteAsync(i.Request, i.Options, TestContext.Current.CancellationToken)).ToList();
        var asyncResponses = await Task.WhenAll(asyncTasks);
        try
        {
            AssertPaired(inputs, asyncResponses, syncTransport.Calls);
        }
        finally
        {
            foreach (var response in asyncResponses)
            {
                response.Dispose();
            }
        }

        // AsBlocking, each call on its own task.
        var asyncTransport = new RecordingTransport(Echo);
        using var asBlocking = asyncTransport.AsBlocking();
        var blockingTasks = inputs
            .Select(i => Task.Run(() => asBlocking.Execute(i.Request, i.Options, CancellationToken.None), TestContext.Current.CancellationToken))
            .ToList();
        var blockingResponses = await Task.WhenAll(blockingTasks);
        try
        {
            AssertPaired(inputs, blockingResponses, asyncTransport.Calls);
        }
        finally
        {
            foreach (var response in blockingResponses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task No_cross_talk_through_both_DelegateHttpClient_forms()
    {
        var inputs = Inputs().ToList();
        var asyncCalls = new System.Collections.Concurrent.ConcurrentBag<RecordedCall>();
        var blockingCalls = new System.Collections.Concurrent.ConcurrentBag<RecordedCall>();
        await using var asyncClient = DelegateHttpClient.Create(async (request, options, ct) =>
        {
            asyncCalls.Add(new RecordedCall(request, options, ct));
            await Task.Yield();
            return Echo(request);
        });
        using var blockingClient = DelegateHttpClient.CreateBlocking((request, options, ct) =>
        {
            blockingCalls.Add(new RecordedCall(request, options, ct));
            return Echo(request);
        });

        var asyncResponses = await Task.WhenAll(inputs.Select(i =>
            Task.Run(() => asyncClient.ExecuteAsync(i.Request, i.Options, CancellationToken.None), TestContext.Current.CancellationToken)));
        var blockingResponses = await Task.WhenAll(inputs.Select(i =>
            Task.Run(() => blockingClient.Execute(i.Request, i.Options, CancellationToken.None), TestContext.Current.CancellationToken)));
        try
        {
            AssertPaired(inputs, asyncResponses, [.. asyncCalls]);
            AssertPaired(inputs, blockingResponses, [.. blockingCalls]);
        }
        finally
        {
            foreach (var response in asyncResponses.Concat(blockingResponses))
            {
                response.Dispose();
            }
        }
    }
}
