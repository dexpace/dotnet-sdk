// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// The composite behind <see cref="RequestBody.Multipart"/> (HTTP-51, BODY-2, BODY-6; design P3b-8).
/// </summary>
/// <remarks>
/// The framing bytes are computed once at construction and used for both <see cref="ContentLength"/> and the write, so the
/// two cannot drift. The body is replayable when every part is, and its length is <c>-1</c> when any part's is. A
/// non-replayable composite carries a consume-once guard, so a second write throws before a byte is written; a replayable
/// one admits concurrent writes. Each part's bytes go through a <see cref="BoundedWriteStream"/> sized to the part's own
/// declared length, so a part that lies about its length is caught: too long, before the extra chunk reaches the sink;
/// too short, when the part finishes. Nothing the composite was given is disposed. Equality is identity.
/// </remarks>
internal sealed class MultipartRequestBody : RequestBody
{
    private static readonly byte[] s_crlf = "\r\n"u8.ToArray();

    private readonly MultipartPart[] _parts;
    private readonly MultipartFraming.Framing _framing;
    private readonly bool _replayable;
    private int _consumed;

    internal MultipartRequestBody(MultipartPart[] parts, string boundary)
    {
        _parts = parts;
        _framing = MultipartFraming.Frame(parts, boundary);
        ContentType = MultipartFraming.ContentTypeFor(boundary);
        _replayable = parts.All(p => p.Body.IsReplayable);
        ContentLength = ComputeLength();
    }

    public override MediaType? ContentType { get; }

    public override long ContentLength { get; }

    public override bool IsReplayable => _replayable;

    public override async Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ClaimIfSingleUse();
        for (var i = 0; i < _parts.Length; i++)
        {
            await destination.WriteAsync(_framing.PartHeaders[i], cancellationToken).ConfigureAwait(false);
            var body = _parts[i].Body;
            var bounded = new BoundedWriteStream(destination, body.ContentLength);
            await using var boundedScope = bounded.ConfigureAwait(false);
            await body.WriteToAsync(bounded, cancellationToken).ConfigureAwait(false);
            bounded.Complete();
            await destination.WriteAsync(s_crlf, cancellationToken).ConfigureAwait(false);
        }

        await destination.WriteAsync(_framing.Trailer, cancellationToken).ConfigureAwait(false);
    }

    public override void WriteTo(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ClaimIfSingleUse();
        for (var i = 0; i < _parts.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(_framing.PartHeaders[i]);
            var body = _parts[i].Body;
            using var bounded = new BoundedWriteStream(destination, body.ContentLength);
            body.WriteTo(bounded, cancellationToken);
            bounded.Complete();
            destination.Write(s_crlf);
        }

        destination.Write(_framing.Trailer);
    }

    // BODY-6: a non-replayable composite refuses a second write before any byte.
    private void ClaimIfSingleUse()
    {
        if (!_replayable && Interlocked.Exchange(ref _consumed, 1) != 0)
        {
            throw new StreamConsumedException(
                "This multipart request body is single-use and has already been written. "
                + "Call ToReplayableAsync() or ToReplayable() before the first send if retries are needed.");
        }
    }

    // -1 when any part's length is unknown; otherwise the stored header bytes, part lengths, CRLFs and the trailer.
    private long ComputeLength()
    {
        long total = _framing.Trailer.Length;
        for (var i = 0; i < _parts.Length; i++)
        {
            var length = _parts[i].Body.ContentLength;
            if (length < 0)
            {
                return -1;
            }

            try
            {
                total = checked(total + _framing.PartHeaders[i].Length + length + s_crlf.Length);
            }
            catch (OverflowException)
            {
                return -1;
            }
        }

        return total;
    }
}
