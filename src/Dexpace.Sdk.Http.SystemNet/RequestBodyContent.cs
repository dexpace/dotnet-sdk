// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// Adapts a <see cref="RequestBody"/> to <see cref="HttpContent"/> so it can be streamed by
/// <see cref="System.Net.Http.HttpClient"/> without first buffering into memory.
/// </summary>
/// <remarks>
/// It carries no <c>Content-Type</c> of its own: <c>OutboundHeaderMapper</c> stamps the body's media type only when the caller set
/// none (<c>TRANSPORT-10</c>). With a <see cref="ResendCapture"/> the first serialisation of a single-use body is teed into the capture and a later one
/// replays it, so a send the native client repeats (a proxy's <c>407</c> answered, a stale connection) carries identical bytes
/// (<c>TRANSPORT-18</c>); without one the body is written as it is, and a single-use body's own latch refuses a second write.
/// </remarks>
internal sealed class RequestBodyContent : HttpContent
{
    private readonly RequestBody _body;
    private readonly ResendCapture? _capture;

    public RequestBodyContent(RequestBody body, ResendCapture? capture = null)
    {
        _body = body;
        _capture = capture;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        switch (_capture?.Begin())
        {
            case null:
                await _body.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
                break;
            case CaptureAction.Record:
                await RecordAsync(stream, cancellationToken).ConfigureAwait(false);
                break;
            case CaptureAction.Replay:
                await _capture!.ReplayAsync(stream, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw _capture!.RefusedResend();
        }
    }

    // The synchronous twin (SEAM-11, TRANSPORT-17; phase 8b): HttpClient.Send writes the body here, on the calling thread, through
    // the body's own WriteTo, so a single-use body is written once and nothing blocks on an asynchronous write.
    protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        switch (_capture?.Begin())
        {
            case null:
                _body.WriteTo(stream, cancellationToken);
                break;
            case CaptureAction.Record:
                Record(stream, cancellationToken);
                break;
            case CaptureAction.Replay:
                _capture!.Replay(stream);
                break;
            default:
                throw _capture!.RefusedResend();
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        var declared = _body.ContentLength;
        if (declared >= 0)
        {
            length = declared;
            return true;
        }

        length = 0;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capture?.Release();
        }

        base.Dispose(disposing);
    }

    private async Task RecordAsync(Stream stream, CancellationToken cancellationToken)
    {
        var tee = new CapturingStream(stream, _capture!);
        await using (tee.ConfigureAwait(false))
        {
            try
            {
                await _body.WriteToAsync(tee, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _capture!.Fail();
                throw;
            }
        }

        _capture!.Complete();
    }

    private void Record(Stream stream, CancellationToken cancellationToken)
    {
        using var tee = new CapturingStream(stream, _capture!);
        try
        {
            _body.WriteTo(tee, cancellationToken);
        }
        catch
        {
            _capture!.Fail();
            throw;
        }

        _capture!.Complete();
    }
}
