// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Subjects;

[Trait("Category", "Unit")]
public sealed class BorrowedTransportTests
{
    private sealed class CountingOwner : IAsyncDisposable
    {
        internal int Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Disposing_releases_the_native_owner_exactly_once_and_never_the_transport()
    {
        var transport = new TrackingTransport();
        var owner = new CountingOwner();
        Func<Uri, CancellationToken, Task> send = (_, _) => Task.CompletedTask;
        var borrowed = new BorrowedTransport(transport, send, owner);

        await borrowed.DisposeAsync();
        await borrowed.DisposeAsync();
        await borrowed.DisposeAsync();

        Assert.Equal(1, owner.Disposed);
        Assert.Equal(0, transport.Disposed);
        Assert.Same(transport, borrowed.Transport);
        Assert.Same(send, borrowed.SendThroughNativeClient);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        await using var transport = new TrackingTransport();
        var owner = new CountingOwner();
        await using var scope = owner;

        Assert.Throws<ArgumentNullException>(() => new BorrowedTransport(null!, (_, _) => Task.CompletedTask, owner));
        Assert.Throws<ArgumentNullException>(() => new BorrowedTransport(transport, null!, owner));
        Assert.Throws<ArgumentNullException>(() => new BorrowedTransport(transport, (_, _) => Task.CompletedTask, null!));
    }

    private sealed class TrackingTransport : IAsyncHttpClient
    {
        internal int Disposed { get; private set; }

        public Task<Response> ExecuteAsync(Dexpace.Sdk.Core.Http.Request.Request request, Dexpace.Sdk.Core.Http.Request.RequestOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}
