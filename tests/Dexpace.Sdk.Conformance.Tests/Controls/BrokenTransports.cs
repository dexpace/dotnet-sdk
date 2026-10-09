// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.RawSocket;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>The building blocks of negative controls: transports that violate exactly one clause.</summary>
internal static class BrokenTransports
{
    /// <summary>
    /// A transport that delegates to a conforming raw-socket client but lets <paramref name="behave"/> change what the caller
    /// sees. <paramref name="behave"/> gets the call and the inner client.
    /// </summary>
    internal static IAsyncHttpClient Around(Func<Request, RequestOptions, CancellationToken, IAsyncHttpClient, Task<Response>> behave)
    {
#pragma warning disable CA2000 // The raw-socket client holds no resources (a connection belongs to its response), so there is nothing to dispose.
        var inner = new RawSocketHttpClient();
#pragma warning restore CA2000
        return DelegateHttpClient.Create((request, options, ct) => behave(request, options, ct, inner));
    }

    /// <summary>A transport that answers with whatever <paramref name="rewrite"/> makes of the raw-socket client's response.</summary>
    internal static IAsyncHttpClient Rewriting(Func<Response, Response> rewrite) =>
        Around(async (request, options, ct, inner) => rewrite(await inner.ExecuteAsync(request, options, ct)));

    /// <summary>A body stream whose disposal does nothing: a response that never releases its connection.</summary>
    internal sealed class LeakyStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // Deliberately does not dispose the inner stream: the socket stays open.
            base.Dispose(disposing);
        }
    }

    /// <summary>A transport whose call throws synchronously on a null request instead of failing the task.</summary>
    internal sealed class SyncThrowingTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(options);
            return Task.FromException<Response>(new InvalidOperationException("never reached"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A transport that completes its task with a null response.</summary>
    internal sealed class NullResponseTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) => Task.FromResult<Response>(null!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
