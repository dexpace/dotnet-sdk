// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>
/// A response body that counts its opens and its disposals, can be told to fail on either, and logs to an ordered list
/// shared with the streams it hands out (SSE-23, P7b-10, P7b-11). A subclass overrides <c>Dispose(bool)</c>, never
/// <c>Dispose()</c> (the dispose latch of 3b).
/// </summary>
internal sealed class SseBody(Func<Stream> open) : ResponseBody
{
    private int _opens;
    private int _asyncOpens;
    private int _syncOpens;
    private int _disposes;

    public Exception? DisposeFailure { get; init; }

    public Exception? OpenFailure { get; init; }

    public Func<CancellationToken, Task<Stream>>? AsyncOpen { get; init; }

    public long Length { get; init; } = -1;

    public List<string>? Log { get; init; }

    public int OpenCount => Volatile.Read(ref _opens);

    public int AsyncOpenCount => Volatile.Read(ref _asyncOpens);

    public int SyncOpenCount => Volatile.Read(ref _syncOpens);

    public int DisposeCount => Volatile.Read(ref _disposes);

    public override MediaType? ContentType => null;

    public override long ContentLength => Length;

    public override async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _opens);
        Interlocked.Increment(ref _asyncOpens);
        Record("body:open-async");
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        return AsyncOpen is null ? open() : await AsyncOpen(cancellationToken);
    }

    public override Stream OpenRead(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _opens);
        Interlocked.Increment(ref _syncOpens);
        Record("body:open-sync");
        return OpenFailure is not null ? throw OpenFailure : open();
    }

    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _disposes);
        Record("body:dispose");
        if (DisposeFailure is not null)
        {
            throw DisposeFailure;
        }

        base.Dispose(disposing);
    }

    private void Record(string entry)
    {
        if (Log is { } log)
        {
            lock (log)
            {
                log.Add(entry);
            }
        }
    }
}

/// <summary>Builds the responses the facade tests consume.</summary>
internal static class SseResponses
{
    internal static Func<Stream> Bytes(string text) => () => new MemoryStream(Encoding.UTF8.GetBytes(text));

    internal static Response Respond(SseBody body, Status? status = null, Method? method = null) =>
        new(
            Request.Create(method ?? Method.Get, "https://sse.example.test/events"),
            status ?? Status.Ok,
            Protocol.Http11,
            body: body);

    internal static (Response Response, SseBody Body) Respond(string text, List<string>? log = null, Exception? disposeFailure = null)
    {
        var body = new SseBody(Bytes(text)) { Log = log, DisposeFailure = disposeFailure };
        return (Respond(body), body);
    }
}

/// <summary>Counts and logs its disposal, then disposes <paramref name="inner"/> and throws <paramref name="failure"/>.</summary>
internal sealed class ThrowingDisposeStream(Stream inner, Exception? failure, List<string>? log = null) : Stream
{
    private int _disposes;

    public int DisposeCount => Volatile.Read(ref _disposes);

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _disposes);
            if (log is not null)
            {
                lock (log)
                {
                    log.Add("stream:dispose");
                }
            }

            inner.Dispose();
            if (failure is not null)
            {
                throw failure;
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Serves <c>initial</c> bytes and then parks every read until the gate opens, like a socket that is waiting for the
/// server. Disposing it completes a parked read with <see cref="ObjectDisposedException"/>, the way a transport tears a
/// read down (SSE-31); with <c>completeOnDispose</c> off it leaves the read parked, so a test can prove the token alone
/// unblocks it.
/// </summary>
internal sealed class GatedStream(byte[] initial, bool completeOnDispose = true, bool honourToken = true) : Stream
{
    private readonly TaskCompletionSource<int> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _served;
    private int _disposes;

    public Task Parked => _parked.Task;

    public int DisposeCount => Volatile.Read(ref _disposes);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (TryServe(buffer.AsSpan(offset, count), out var n))
        {
            return n;
        }

        _parked.TrySetResult();
        return _gate.Task.GetAwaiter().GetResult();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (TryServe(buffer.Span, out var n))
        {
            return n;
        }

        _parked.TrySetResult();
        return honourToken ? await _gate.Task.WaitAsync(cancellationToken) : await _gate.Task;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _disposes);
            if (completeOnDispose)
            {
                _gate.TrySetException(new ObjectDisposedException(nameof(GatedStream)));
            }
        }

        base.Dispose(disposing);
    }

    private bool TryServe(Span<byte> buffer, out int n)
    {
        n = 0;
        if (_served >= initial.Length)
        {
            return false;
        }

        n = Math.Min(buffer.Length, initial.Length - _served);
        initial.AsSpan(_served, n).CopyTo(buffer);
        _served += n;
        return true;
    }
}

/// <summary>
/// Yields its bytes and then throws <paramref name="failure"/> from every further read, the shape of a connection that
/// drops mid-stream (SSE-29, SSE-40).
/// </summary>
internal sealed class FailingAfterStream(byte[] bytes, Exception failure) : Stream
{
    private int _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_position >= bytes.Length)
        {
            throw failure;
        }

        var n = Math.Min(buffer.Length, bytes.Length - _position);
        bytes.AsSpan(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return new ValueTask<int>(Read(buffer.Span));
        }
        catch (Exception ex) when (ex == failure)
        {
            return ValueTask.FromException<int>(ex);
        }
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// Yields its bytes and then parks every further read until the caller's token is cancelled, so a test can prove the
/// enumeration token reaches the read (SSE-40).
/// </summary>
internal sealed class ParkedAfterStream(byte[] bytes) : Stream
{
    private int _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position < bytes.Length)
        {
            var n = Math.Min(buffer.Length, bytes.Length - _position);
            bytes.AsMemory(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
