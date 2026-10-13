// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>A delegating handler a test adds through <c>HttpClient.AddHttpMessageHandler</c> to read the chain below it and to see what reaches it.</summary>
internal sealed class ChainSpy : DelegatingHandler
{
    private int _disposals;

    /// <summary>How many times the factory or the container disposed this handler.</summary>
    internal int Disposals => Volatile.Read(ref _disposals);

    /// <summary>The requests that reached this handler.</summary>
    internal List<HttpRequestMessage> Seen { get; } = [];

    /// <summary>The handlers below this one, in order, ending with the primary.</summary>
    internal IReadOnlyList<HttpMessageHandler> Below()
    {
        var chain = new List<HttpMessageHandler>();
        for (var handler = InnerHandler; handler is not null; handler = (handler as DelegatingHandler)?.InnerHandler)
        {
            chain.Add(handler);
        }

        return chain;
    }

    /// <summary>The primary handler at the bottom of the chain.</summary>
    internal HttpMessageHandler Primary => Below()[^1];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Seen)
        {
            Seen.Add(request);
        }

        return base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _disposals);
        }

        base.Dispose(disposing);
    }
}
