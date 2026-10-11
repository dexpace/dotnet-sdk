// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Generic;
using System.Linq;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>What <see cref="ResendCapture.Begin"/> decided a serialisation of a single-use body must do.</summary>
internal enum CaptureAction
{
    /// <summary>This is the first serialisation: write the body to the wire and keep a copy of what was written.</summary>
    Record,

    /// <summary>The first serialisation completed within the limit: write the kept copy, never the source.</summary>
    Replay,

    /// <summary>The copy is unusable (the first write failed, or the body outgrew the limit): refuse the re-send.</summary>
    Refuse,
}

/// <summary>
/// A bounded copy of a single-use request body's first serialisation, so a re-send by the native client can replay the same
/// bytes instead of failing (<c>TRANSPORT-18</c>; design §F, phase 8b P8b-10; facts G5 to G7).
/// </summary>
/// <remarks>
/// <para>
/// The native client sends a request again after a proxy's <c>407</c> answered with credentials, after an authentication challenge, or
/// after a connection failure before the body started, and each send serialises the content again. A single-use body cannot be
/// read twice, so the first serialisation is teed into this capture (at most <c>ResendBufferLimit</c> bytes) and a later one replays
/// it. If the first write did not complete, or the body outgrew the limit, the capture is unusable and a later serialisation is
/// refused: the send fails with the transport-failure type and never ships a truncated body.
/// </para>
/// <para>
/// Chunks are plain unpooled arrays (<c>IO-22</c>: captured body bytes are never pooled). The state machine is guarded by a lock;
/// one request's serialisations are sequential in practice, and the lock keeps a misbehaving handler from corrupting the copy.
/// </para>
/// </remarks>
internal sealed class ResendCapture
{
    // Below the large object heap threshold, so a captured body never lands on it chunk by chunk.
    private const int ChunkBytes = 80_000;
    private const int FirstChunkBytes = 4_096;

    private readonly long _limit;
    private readonly List<Chunk> _chunks = [];
    private readonly object _gate = new();
    private State _state = State.NotStarted;
    private long _total;
    private long _stored;

    internal ResendCapture(long limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        _limit = limit;
    }

    private enum State
    {
        NotStarted,
        Recording,
        Complete,
        Unusable,
    }

    /// <summary>Whether the copy was released (the request content was disposed).</summary>
    internal bool Released { get; private set; }

    /// <summary>The limit this capture was built with, in bytes.</summary>
    internal long Limit => _limit;

    /// <summary>Decides what the serialisation that is starting must do, and moves the state on.</summary>
    internal CaptureAction Begin()
    {
        lock (_gate)
        {
            switch (_state)
            {
                case State.NotStarted:
                    _state = State.Recording;
                    return CaptureAction.Record;
                case State.Complete:
                    return CaptureAction.Replay;
                default:
                    // Recording (a second serialisation began before the first finished) is as unusable as a failed first.
                    _state = State.Unusable;
                    DropChunks();
                    return CaptureAction.Refuse;
            }
        }
    }

    /// <summary>Appends what the first serialisation wrote; once the limit is passed the copy is dropped and the capture unusable.</summary>
    internal void Append(ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            if (_state != State.Recording)
            {
                return;
            }

            _total += bytes.Length;
            if (_total > _limit)
            {
                _state = State.Unusable;
                DropChunks();
                return;
            }

            while (!bytes.IsEmpty)
            {
                if (_chunks.Count == 0 || _chunks[^1].Used == _chunks[^1].Buffer.Length)
                {
                    // Small bodies pay for a small first chunk; later chunks grow to ChunkBytes; none exceeds what the limit still allows.
                    var size = (int)Math.Min(_limit - _stored, _chunks.Count == 0 ? Math.Max(FirstChunkBytes, bytes.Length) : ChunkBytes);
                    _chunks.Add(new Chunk(new byte[Math.Min(size, ChunkBytes)]));
                }

                var chunk = _chunks[^1];
                var take = Math.Min(chunk.Buffer.Length - chunk.Used, bytes.Length);
                bytes[..take].CopyTo(chunk.Buffer.AsSpan(chunk.Used));
                chunk.Used += take;
                _stored += take;
                bytes = bytes[take..];
            }
        }
    }

    /// <summary>The first serialisation returned normally within the limit: the copy is complete.</summary>
    internal void Complete()
    {
        lock (_gate)
        {
            if (_state == State.Recording)
            {
                _state = State.Complete;
            }
        }
    }

    /// <summary>The first serialisation did not complete: the copy is unusable.</summary>
    internal void Fail()
    {
        lock (_gate)
        {
            if (_state == State.Recording)
            {
                _state = State.Unusable;
                DropChunks();
            }
        }
    }

    /// <summary>Writes the kept copy, byte for byte.</summary>
    internal void Replay(Stream destination)
    {
        foreach (var (chunk, count) in Snapshot())
        {
            destination.Write(chunk, 0, count);
        }
    }

    /// <summary>Writes the kept copy, byte for byte.</summary>
    internal async Task ReplayAsync(Stream destination, CancellationToken cancellationToken)
    {
        foreach (var (chunk, count) in Snapshot())
        {
            await destination.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Frees the copy. Called when the request content is disposed.</summary>
    internal void Release()
    {
        lock (_gate)
        {
            Released = true;
            DropChunks();
            if (_state != State.Unusable)
            {
                _state = State.Unusable;
            }
        }
    }

    /// <summary>
    /// The failure a refused re-send throws: an <see cref="IOException"/>, so <c>HttpClient</c> wraps it in an
    /// <see cref="HttpRequestException"/> (it leaves most other exceptions unwrapped, which would bypass the transport's
    /// classification), over a <see cref="StreamConsumedException"/> that names the limit and the remedy.
    /// </summary>
    internal IOException RefusedResend() => new(
        "The transport had to send a single-use request body again but could not replay it. Its first write did not complete or the "
        + "body was larger than ResendBufferLimit (" + _limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + " bytes); raise SystemNetHttpClientOptions.ResendBufferLimit or buffer the body with ToReplayableAsync before sending.",
        new StreamConsumedException(
            "A single-use request body was serialised a second time after its replay copy was refused (ResendBufferLimit, TRANSPORT-18); "
            + "buffer it with ToReplayableAsync first."));

    private (byte[] Chunk, int Count)[] Snapshot()
    {
        lock (_gate)
        {
            return [.. _chunks.Select(chunk => (chunk.Buffer, chunk.Used))];
        }
    }

    private void DropChunks()
    {
        _chunks.Clear();
        _stored = 0;
    }

    private sealed class Chunk(byte[] buffer)
    {
        internal byte[] Buffer { get; } = buffer;

        internal int Used { get; set; }
    }
}
