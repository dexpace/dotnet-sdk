// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>A hand-driven TCP peer for the fixture's own tests: the bytes it sends and reads are exactly the ones asserted on.</summary>
internal sealed class RawTcp : IDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;

    internal NetworkStream Stream => _stream ?? throw new InvalidOperationException("Connect first.");

    internal static async Task<RawTcp> ConnectAsync(LoopbackServer server, CancellationToken cancellationToken)
    {
        var peer = new RawTcp();
        await peer._client.ConnectAsync(IPAddress.Loopback, server.BaseUri.Port, cancellationToken);
        peer._stream = peer._client.GetStream();
        return peer;
    }

    internal Task SendAsync(string text, CancellationToken cancellationToken) =>
        Stream.WriteAsync(Encoding.Latin1.GetBytes(text), cancellationToken).AsTask();

    /// <summary>Reads until the text read so far ends with <paramref name="marker"/>, or the peer closes.</summary>
    internal async Task<string> ReadUntilAsync(string marker, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new byte[4096];
        while (!text.ToString().EndsWith(marker, StringComparison.Ordinal))
        {
            var read = await Stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            text.Append(Encoding.Latin1.GetString(buffer, 0, read));
        }

        return text.ToString();
    }

    /// <summary>Reads exactly <paramref name="count"/> bytes, or fewer if the peer closes first.</summary>
    internal async Task<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var filled = 0;
        while (filled < count)
        {
            var read = await Stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);
            if (read == 0)
            {
                return buffer[..filled];
            }

            filled += read;
        }

        return buffer;
    }

    /// <summary>
    /// Whether, after <paramref name="window"/>, the peer has sent nothing and has not closed: the fixture's "still gated"
    /// check. It polls the socket instead of reading it, so no byte of a later reply is consumed.
    /// </summary>
    internal async Task<bool> StaysSilentAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        await TimeProvider.System.DelayAsync(window, cancellationToken);
        return !_client.Client.Poll(0, SelectMode.SelectRead);
    }

    public void Dispose() => _client.Dispose();
}
