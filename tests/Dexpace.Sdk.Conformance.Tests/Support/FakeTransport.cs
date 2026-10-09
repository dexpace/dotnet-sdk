// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>A transport on both faces that records which member ran, on which kind of thread, and how often it was disposed.</summary>
internal sealed class FakeTransport : IAsyncHttpClient, IHttpClient
{
    private int _asyncCalls;
    private int _blockingCalls;
    private int _disposed;

    internal int AsyncCalls => Volatile.Read(ref _asyncCalls);

    internal int BlockingCalls => Volatile.Read(ref _blockingCalls);

    internal int Disposed => Volatile.Read(ref _disposed);

    internal bool LastBlockingCallOnPoolThread { get; private set; }

    internal Func<Request, CancellationToken, Task<Response>> OnAsync { get; set; } = (request, _) => Task.FromResult(Ok(request));

    internal Func<Request, CancellationToken, Response> OnBlocking { get; set; } = (request, _) => Ok(request);

    internal static Response Ok(Request request) =>
        new(request, Status.Ok, Dexpace.Sdk.Core.Http.Common.Protocol.Http11, null, ResponseBody.FromBytes(Array.Empty<byte>()));

    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _asyncCalls);
        return OnAsync(request, cancellationToken);
    }

    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _blockingCalls);
        LastBlockingCallOnPoolThread = Thread.CurrentThread.IsThreadPoolThread;
        return OnBlocking(request, cancellationToken);
    }

    public void Dispose() => Interlocked.Increment(ref _disposed);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Builds subjects over <see cref="FakeTransport"/> and keeps what they built, so a test can count builds and disposals.</summary>
internal sealed class FakeSubject
{
    private readonly List<FakeTransport> _built = [];

    internal IReadOnlyList<FakeTransport> Built
    {
        get
        {
            lock (_built)
            {
                return [.. _built];
            }
        }
    }

    internal Action<FakeTransport>? Configure { get; init; }

    internal TransportSubject Subject(bool async = true, bool blocking = true) => new()
    {
        Name = "fake",
        CreateAsync = async ? _ => Build() : null,
        CreateBlocking = blocking ? _ => Build() : null,
    };

    private FakeTransport Build()
    {
        var transport = new FakeTransport();
        Configure?.Invoke(transport);
        lock (_built)
        {
            _built.Add(transport);
        }

        return transport;
    }
}
