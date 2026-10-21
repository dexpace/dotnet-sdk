// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.ServerSentEvents;

namespace Dexpace.Sdk.AotSmoke;

// Phase 11 (observability, SSE): the trace-id generators draw from RandomNumberGenerator and format through Span helpers, and the per-event cap runs through the
// reader's accumulator and the facade's async iterator. Both must survive trimming and NativeAOT, so the published binary draws ids and trips the cap.
internal static partial class SmokeChecks
{
    private static async Task CheckPhase11ObservabilityAsync()
    {
        // OBS-27, P11-18: a Datadog id has a zero high half and a non-zero low half and renders as an unsigned decimal; a W3C id is 32 hex and never all-zero.
        for (var i = 0; i < 256; i++)
        {
            var datadog = TraceIdGeneration.Datadog();
            var hex = datadog.ToHexString();
            Expect(hex.Length == 32 && hex.AsSpan(0, 16).IndexOfAnyExcept('0') < 0 && hex.AsSpan(16).IndexOfAnyExcept('0') >= 0, "TraceIdGeneration.Datadog draws a zero high half and a non-zero low half");
            Expect(
                ulong.TryParse(TraceIdGeneration.FormatDatadog(datadog), NumberStyles.None, CultureInfo.InvariantCulture, out var decimalId)
                && decimalId == ulong.Parse(hex.AsSpan(16), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
                && decimalId != 0,
                "TraceIdGeneration.FormatDatadog renders the low half as a non-zero unsigned decimal");

            var w3c = TraceIdGeneration.W3C().ToHexString();
            Expect(w3c.Length == 32 && w3c.AsSpan().IndexOfAnyExcept('0') >= 0, "TraceIdGeneration.W3C draws a 32-hex id that is not all-zero");
        }

        Expect(TraceIdGeneration.FormatDatadog(ActivityTraceId.CreateFromString("0000000000000000ffffffffffffffff")) == "18446744073709551615", "FormatDatadog renders the largest 64-bit id");

        // SSE-19, P11-19: lines that each fit but never end the event are refused once the event's data passes the cap, by the bare reader and through the facade.
        var wire = string.Concat(Enumerable.Repeat("data:x\n", 400));
        var reader = new ServerSentEventReader(new MemoryStream(Encoding.UTF8.GetBytes(wire)), ServerSentEventReader.DefaultMaxLineBytes, 256);
        var readerCapped = false;
        try
        {
            await reader.ReadNextAsync();
        }
        catch (ServerSentEventTooLargeException ex)
        {
            readerCapped = ex.MaxEventBytes == 256;
        }

        Expect(readerCapped, "ServerSentEventReader refuses an event whose data passes maxEventBytes");

        using var body = new EventStreamBody(wire);
        var response = new Response(Request.Get("https://smoke.example.test/events"), Status.Ok, Protocol.Http11, body: body);
        var streamCapped = false;
        await using (var events = ServerSentEventStream.FromResponse(response, ServerSentEventReader.DefaultMaxLineBytes, 256))
        {
            try
            {
                await foreach (var ignored in events)
                {
                    Expect(ignored is null, "no event is delivered from an over-cap block");
                }
            }
            catch (ServerSentEventTooLargeException ex)
            {
                streamCapped = ex.MaxEventBytes == 256;
            }
        }

        Expect(streamCapped && body.Releases == 1, "ServerSentEventStream surfaces the cap failure and releases the response once");

        // A block under the cap is delivered whole.
        var small = new ServerSentEventReader(new MemoryStream(Encoding.UTF8.GetBytes("data:a\ndata:b\n\n")), ServerSentEventReader.DefaultMaxLineBytes, 256);
        var delivered = await small.ReadNextAsync();
        Expect(delivered is { Data: ["a", "b"] }, "an event under the cap is delivered whole");
    }
}
