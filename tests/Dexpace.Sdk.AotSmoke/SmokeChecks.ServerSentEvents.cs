// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.ServerSentEvents;

namespace Dexpace.Sdk.AotSmoke;

// Phase 7b: server-sent events. The async iterators, the single-use latches, the record-struct conversion and the typed
// adapter must survive trimming and NativeAOT, so the check runs the facade, the typed adapter and the bare reader.
internal static partial class SmokeChecks
{
    // A comment, two events (the second ends in CR CR), the caller's sentinel, and an event that must never be mapped.
    private const string EventStream =
        ": hi\n\ndata: {\"name\":\"a\"}\r\rdata: {\"name\":\"b\"}\n\ndata: [DONE]\n\ndata: {\"name\":\"never\"}\n\n";

    private static async Task CheckServerSentEventsAsync()
    {
        // Asynchronous: pipeline, then the facade, then MapAsync with a source-generated JsonTypeInfo.
        using var asyncBody = new EventStreamBody(EventStream);
        await using var asyncTransport = DelegateHttpClient.Create((request, _, _) =>
            Task.FromResult(new Response(request, Status.Ok, Protocol.Http11, body: asyncBody)));
        using var asyncPipeline = DexpacePipeline.CreateDefault(asyncTransport);
        var asyncResponse = await asyncPipeline.SendAsync(Request.Get("https://smoke.example.test/events"), CancellationToken.None);
        var asyncNames = new List<string>();
        var asyncCalls = 0;
        await using (var events = ServerSentEventStream.FromResponse(asyncResponse))
        {
            await foreach (var chunk in events.MapAsync<SmokeChunk>((_, data) => MapChunk(data, ref asyncCalls)))
            {
                asyncNames.Add(chunk.Name);
            }
        }

        Expect(asyncNames is ["a", "b"], "MapAsync yields a and b, skips the comment and stops at the sentinel");
        Expect(asyncCalls == 4, "MapAsync maps the comment, a, b and the sentinel and never the event after it");
        Expect(asyncBody.Releases == 1 && asyncBody.AsyncOpens == 1 && asyncBody.SyncOpens == 0, "The async view releases once, opened asynchronously");

        // Blocking: HttpPipeline.Send, then Map over OpenRead.
        using var blockingBody = new EventStreamBody(EventStream);
        await using var blockingTransport = DelegateHttpClient.Create((request, _, _) =>
            Task.FromResult(new Response(request, Status.Ok, Protocol.Http11, body: blockingBody)));
        using var blockingPipeline = DexpacePipeline.CreateDefault(blockingTransport);
        var blockingResponse = blockingPipeline.Send(Request.Get("https://smoke.example.test/events"), CancellationToken.None);
        var blockingNames = new List<string>();
        var blockingCalls = 0;
        using (var events = ServerSentEventStream.FromResponse(blockingResponse))
        {
            foreach (var chunk in events.Map<SmokeChunk>((_, data) => MapChunk(data, ref blockingCalls)))
            {
                blockingNames.Add(chunk.Name);
            }
        }

        Expect(blockingNames is ["a", "b"] && blockingCalls == 4, "Map yields a and b over the blocking path");
        Expect(blockingBody.Releases == 1 && blockingBody.SyncOpens == 1 && blockingBody.AsyncOpens == 0, "The blocking view releases once, opened synchronously");

        // The bare reader: a leading BOM is consumed once, a CR ends a line at once.
        var reader = new List<ServerSentEvent>();
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. "data: x\r\rdata: y\n\n"u8.ToArray()];
        await foreach (var ev in ServerSentEventReader.ReadAllAsync(new MemoryStream(bytes)))
        {
            reader.Add(ev);
        }

        Expect(reader.Count == 2 && reader[0].Data[0] == "x" && reader[1].Data[0] == "y", "ServerSentEventReader.ReadAllAsync strips the BOM and splits on CR");

        // The facade rejects a bodyless response and owns (releases) it.
        using var rejected = new EventStreamBody(string.Empty);
        var rejectedResponse = new Response(Request.Get("https://smoke.example.test/events"), Status.NoContent, Protocol.Http11, body: rejected);
        var threw = false;
        try
        {
            _ = ServerSentEventStream.FromResponse(rejectedResponse);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        Expect(threw && rejected.Releases == 1, "FromResponse rejects a 204 and releases the response");
    }

    // The sentinel is the caller's: this mapper names it, core never does. An empty data string is the comment-only block.
    private static SseMapResult<SmokeChunk> MapChunk(string data, ref int calls)
    {
        calls++;
        if (data.Length == 0)
        {
            return SseMapResult.Skip;
        }

        return data == "[DONE]"
            ? SseMapResult.Done
            : SseMapResult.Value(JsonSerializer.Deserialize(data, SmokeJsonContext.Default.SmokeChunk)!);
    }

    /// <summary>A body that serves event-stream bytes and counts its opens (by form) and its releases.</summary>
    private sealed class EventStreamBody(string text) : ResponseBody
    {
        private int _releases;
        private int _asyncOpens;
        private int _syncOpens;

        public int Releases => Volatile.Read(ref _releases);

        public int AsyncOpens => Volatile.Read(ref _asyncOpens);

        public int SyncOpens => Volatile.Read(ref _syncOpens);

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _asyncOpens);
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        }

        public override Stream OpenRead(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _syncOpens);
            return new MemoryStream(Encoding.UTF8.GetBytes(text));
        }

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _releases);
            base.Dispose(disposing);
        }
    }
}
