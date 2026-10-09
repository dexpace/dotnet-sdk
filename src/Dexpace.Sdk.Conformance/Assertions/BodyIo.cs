// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Reading a response body the way the face's caller would, bounded: asynchronously on the async face, through the
/// synchronous stream on the blocking face (offloaded to a pool thread, so a blocked read is a failure naming the bound and
/// not a hung run).
/// </summary>
internal static class BodyIo
{
    /// <summary>Opens the body in the face's form.</summary>
    internal static Task<Stream> OpenAsync(Response response, TransportFace face, CancellationToken cancellationToken) =>
        Check.GuardAsync(
            async () => face == TransportFace.Blocking
                ? await Task.Run(() => response.Body.OpenRead(cancellationToken), CancellationToken.None).ConfigureAwait(false)
                : await response.Body.OpenReadAsync(cancellationToken).ConfigureAwait(false),
            "opening the response body",
            cancellationToken);

    /// <summary>Reads into <paramref name="buffer"/>; zero is the end of the body. A read that outlives <paramref name="bound"/> is a failure naming <paramref name="what"/>.</summary>
    internal static async Task<int> ReadAsync(Stream stream, byte[] buffer, TransportFace face, TimeSpan bound, string what, CancellationToken cancellationToken)
    {
        var read = face == TransportFace.Blocking
            ? Task.Run(() => stream.Read(buffer, 0, buffer.Length), CancellationToken.None)
            : stream.ReadAsync(buffer.AsMemory(), cancellationToken).AsTask();
        await Bounded.WaitAsync(read, bound, what, cancellationToken).ConfigureAwait(false);
        return await Check.GuardAsync(() => read, what, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the whole body into <paramref name="sink"/>, a chunk at a time.</summary>
    internal static async Task<long> DrainAsync(Stream stream, Action<ReadOnlyMemory<byte>> sink, TransportFace face, TimeSpan bound, string what, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await ReadAsync(stream, buffer, face, bound, what, cancellationToken).ConfigureAwait(false)) > 0)
        {
            sink(buffer.AsMemory(0, read));
            total += read;
        }

        return total;
    }
}
