// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// The request-logging wrapper (BODY-17 to BODY-21, BODY-32, BODY-34, BODY-37; design P3b-9): it delegates the write to the
/// wrapped body through a <see cref="TeeStream"/> so a bounded preview of what went to the wire is available afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Internal, with a required cap: the instrumentation of phase 5b is its only consumer and engages it only when body
/// logging is on. Every write clears the tap and mirrors up to the cap while forwarding every byte (BODY-17 to BODY-19), so
/// the preview reflects the latest attempt only; the tee mirrors before it forwards, so a failed write leaves the failing
/// chunk captured (BODY-20). The wrapper hands out no tap, only copying snapshots (BODY-37).
/// </para>
/// <para>
/// <see cref="IsReplayable"/> is the delegate's; <see cref="ToReplayableAsync"/> returns this when the delegate is
/// replayable and otherwise a new wrapper, with its own tap and the same cap, over the delegate's replayable form
/// (BODY-21). Two concurrent writes of one wrapped replayable body would interleave the preview; a call has one attempt in
/// flight at a time. Equality is identity.
/// </para>
/// </remarks>
internal sealed class LoggingRequestBody : RequestBody
{
    private readonly RequestBody _inner;
    private readonly int _tapCap;
    private TeeStream? _tee;

    /// <summary>Initializes a new wrapper.</summary>
    /// <param name="inner">The body to wrap.</param>
    /// <param name="tapCap">The most bytes the preview keeps; 0 mirrors nothing. A value above <see cref="Array.MaxLength"/> is clamped.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tapCap"/> is negative (BODY-32).</exception>
    internal LoggingRequestBody(RequestBody inner, int tapCap)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(tapCap);
        _inner = inner;
        _tapCap = Math.Min(tapCap, Array.MaxLength);
    }

    public override MediaType? ContentType => _inner.ContentType;

    public override long ContentLength => _inner.ContentLength;

    public override bool IsReplayable => _inner.IsReplayable;

    /// <summary>A copy of what the latest write mirrored, up to the cap.</summary>
    /// <returns>A new array; empty before the first write.</returns>
    internal byte[] Snapshot() => Volatile.Read(ref _tee)?.SnapshotTap() ?? [];

    /// <summary>A copy of the first <paramref name="maxBytes"/> mirrored bytes, or fewer if fewer exist.</summary>
    /// <param name="maxBytes">The most bytes to return; above <see cref="Array.MaxLength"/> is clamped.</param>
    /// <returns>A new array.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative (BODY-32).</exception>
    internal byte[] Snapshot(int maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var all = Snapshot();
        return maxBytes >= all.Length ? all : all[..maxBytes];
    }

    public override async Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var tee = StartAttempt(destination);
        await using var teeScope = tee.ConfigureAwait(false);
        await _inner.WriteToAsync(tee, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteTo(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var tee = StartAttempt(destination);
        _inner.WriteTo(tee, cancellationToken);
    }

    public override async Task<RequestBody> ToReplayableAsync(CancellationToken cancellationToken = default) =>
        _inner.IsReplayable
            ? this
            : new LoggingRequestBody(await _inner.ToReplayableAsync(cancellationToken).ConfigureAwait(false), _tapCap);

    public override RequestBody ToReplayable(CancellationToken cancellationToken = default) =>
        _inner.IsReplayable ? this : new LoggingRequestBody(_inner.ToReplayable(cancellationToken), _tapCap);

    // BODY-18: a fresh tee per attempt clears the tap; the tee leaves the destination open (IO-6).
    private TeeStream StartAttempt(Stream destination)
    {
        var tee = new TeeStream(destination, _tapCap, leaveOpen: true);
        Volatile.Write(ref _tee, tee);
        return tee;
    }
}
