// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/xcut/diagnostic-previews.conformance.test.ts and security-by-default.conformance.test.ts,
// and ruby-sdk@5b17395 gems/dexpace-conformance/lib/dexpace/conformance/invariant_suite/ (observability, entropy, ownership, resolution,
// previews). XCUT-24's non-consuming clause is declined for an error-body snapshot above 1 MiB (P10-23) and is not asserted for it.
using System.Globalization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>XCUT-20</c> to <c>XCUT-24</c> and the scheduler-agnostic clause of <c>NFR-11</c>: observability that never throws into the
/// request path, unpredictable and non-repeating nonces, components that close only what they created, a seam that resolves
/// deterministically, capped non-consuming previews, and a call that completes on whatever scheduler the caller runs it on.
/// </summary>
internal static class HostInvariants
{
    private const int Draws = 10_000;
    private const int MinimumNonceBits = 128;
    private const long LargeBody = 10L * 1024 * 1024;
    private const int PreviewCap = 1024;
    private const int ReadAhead = 64 * 1024;
    private const string CnonceMarker = "cnonce=\"";

    private static readonly string[] s_malformedUrls =
    [
        string.Empty,
        "not a url at all",
        "http://[::1",
        "http://svc-user-9f3a:p4ss-9f3a@[bad",
        "ht!tp://x/y",
        "http://exa mple.com/%zz",
    ];

    internal static ConformanceAssertion[] Assertions() =>
    [
        InvariantAssertionBuilder.Define("xcut-20.observability-never-throws", ["XCUT-20"], RequirementLevel.Must, InvariantAssertionBuilder.Both, ObservabilityNeverThrowsAsync),
        InvariantAssertionBuilder.Define("xcut-21.cnonce-unpredictable", ["XCUT-21"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, CnonceUnpredictableAsync),
        InvariantAssertionBuilder.Define("xcut-22.core-borrows", ["XCUT-22"], RequirementLevel.Must, InvariantAssertionBuilder.Both, CoreBorrowsAsync),
        InvariantAssertionBuilder.Define("xcut-23.resolution-deterministic", ["XCUT-23"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, ResolutionDeterministicAsync),
        InvariantAssertionBuilder.Define("xcut-24.preview-capped-non-consuming", ["XCUT-24"], RequirementLevel.Should, InvariantAssertionBuilder.Both, PreviewCappedNonConsumingAsync),
        InvariantAssertionBuilder.Define("nfr-11.scheduler-agnostic", ["NFR-11"], RequirementLevel.Should, InvariantAssertionBuilder.Both, SchedulerAgnosticAsync),
    ];

    // A malformed URL renders a placeholder, and a sink that fails on every touch does not fail the request.
    private static async Task ObservabilityNeverThrowsAsync(InvariantContext context, CancellationToken token)
    {
        foreach (var url in s_malformedUrls)
        {
            string? rendered = null;
            Exception? thrown = null;
            try
            {
                rendered = context.Subject.RedactUrl(url);
            }
#pragma warning disable CA1031 // The point of the check: whatever the renderer raises for hostile input is the answer.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                thrown = ex;
            }

            Check.True(thrown is null, "rendering a malformed URL must not throw into the caller's path", "a placeholder", Outcome.Name(thrown));
            Check.True(rendered is not null && !rendered.Contains("p4ss-9f3a", StringComparison.Ordinal), "a malformed URL must render as a safe placeholder that does not echo its secrets", "a placeholder", rendered ?? "null");
        }

        var failing = new ThrowingLogger();
        var transport = context.Transport(ProbeTransport.Behavior.Of((request, _) => new Response(
            request,
            Status.Ok,
            Protocol.Http11,
            new Headers.Builder().Add("Content-Type", "text/plain").Build(),
            ResponseBody.FromBytes("ok"u8.ToArray()),
            null)));
        var options = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0), Logging = new HttpLoggingOptions { Level = HttpLogLevel.Body } };
        var pipeline = context.Subject.CreatePipeline(transport, options, context.Time, failing);

        var failure = await context.FailureOfAsync(pipeline, Request.Post(InvariantOptions.Url, RequestBody.FromBytes("payload"u8.ToArray())), "a call logged to a sink that fails every time", token).ConfigureAwait(false);

        Check.True(failure is null, "a failing log sink must not fail the request: instrumentation degrades and the request proceeds", "success", Outcome.Name(failure));
        if (failing.Touches == 0)
        {
            throw new ConformanceVacuousException("the pipeline never touched the failing log sink at the Body level, so there was no sink failure to survive");
        }
    }

    // At least 128 bits, in hex of either case or in base64 or base64url, and never repeating over ten thousand draws. The source clause (the
    // operating system's CSPRNG) is not an output property: it is cited by reference to DigestComputationTests.The_cnonce_source_is_RandomNumberGenerator.
    private static Task CnonceUnpredictableAsync(InvariantContext context, CancellationToken token)
    {
        var draw = context.Subject.DrawCnonce ?? DigestDraw();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Draws; i++)
        {
            var value = draw() ?? string.Empty;
            var bits = NonceBits(value);
            Check.True(
                bits >= MinimumNonceBits,
                "a nonce must carry at least 128 bits (written as hex, base64 or base64url, so a longer draw or another case also counts)",
                $"at least {MinimumNonceBits} bits",
                bits == 0 ? $"{value.Length} characters that are neither hex nor base64" : $"{bits} bits in {value.Length} characters");
            if (!seen.Add(value))
            {
                Check.Fail("a nonce must not repeat: a strong source does not repeat within ten thousand draws", "no repeat", $"a repeat at draw {i + 1}");
            }
        }

        return Task.CompletedTask;
    }

    // The whole bytes a nonce spells: 4 bits a hex digit, 6 bits a base64 or base64url character (padding is not a character). Anything else is
    // not countable and reads as 0, so an encoding the suite cannot measure fails instead of passing.
    private static int NonceBits(string value)
    {
        if (value.Length > 0 && value.All(Uri.IsHexDigit))
        {
            return value.Length * 4 / 8 * 8;
        }

        var characters = value.TrimEnd('=');
        return characters.Length > 0 && characters.All(static c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/' or '-' or '_')
            ? characters.Length * 6 / 8 * 8
            : 0;
    }

    private static Func<string> DigestDraw()
    {
        var handler = new DigestChallengeHandler(new DigestCredential("svc-user", "svc-secret"));
        var challenges = AuthenticationChallenge.Parse("Digest realm=\"probe\", nonce=\"abc123\", qop=\"auth\", algorithm=SHA-256");
        var request = Request.Get(InvariantOptions.Url);
        return () =>
        {
            var authorized = handler.Authorize(challenges, request, proxy: false)
                ?? throw new ConformanceException("the Digest handler produced no Authorization for a satisfiable challenge");
            var header = authorized.Headers.Get("Authorization") ?? string.Empty;
            var start = header.IndexOf(CnonceMarker, StringComparison.Ordinal);
            var end = start < 0 ? -1 : header.IndexOf('"', start + CnonceMarker.Length);
            return start < 0 || end < 0
                ? throw new ConformanceException("the Digest Authorization carried no cnonce")
                : header[(start + CnonceMarker.Length)..end];
        };
    }

    // The SDK closes only what it created: a pipeline and the two bridges over a caller's transport dispose nothing of it.
    private static async Task CoreBorrowsAsync(InvariantContext context, CancellationToken token)
    {
        var transport = context.Transport(ProbeTransport.Behavior.Status(200));
        var pipeline = context.Build(transport, InvariantOptions.Retrying(0));
        var failure = await context.FailureOfAsync(pipeline, Request.Get(InvariantOptions.Url), "a call through a pipeline over a borrowed transport", token).ConfigureAwait(false);
        Check.True(failure is null, "a call over a borrowed transport must succeed", "success", Outcome.Name(failure));
        Check.Equal(transport.Disposals, 0, "the transports disposed by a call through a pipeline over them");

        if (context.Face == TransportFace.Blocking)
        {
            pipeline.Dispose();
        }
        else
        {
            await pipeline.DisposeAsync().ConfigureAwait(false);
        }

        Check.Equal(transport.Disposals, 0, "the transports disposed when the pipeline over them was disposed");

        var bridged = context.Subject.AsAsync(transport);
        using (var response = await Check.GuardAsync(() => bridged.ExecuteAsync(Request.Get(InvariantOptions.Url), token), "a call through the asynchronous bridge", token).ConfigureAwait(false))
        {
            Check.Equal(response.Status.Code, 200, "the status through the asynchronous bridge");
        }

        await bridged.DisposeAsync().ConfigureAwait(false);
        Check.Equal(transport.Disposals, 0, "the transports disposed when the asynchronous bridge over them was disposed");

        var blocking = context.Subject.AsBlocking(transport);
        using (var response = await Check.GuardAsync(
            () => Task.Factory.StartNew(() => blocking.Execute(Request.Get(InvariantOptions.Url), token), CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default),
            "a call through the blocking bridge",
            token).ConfigureAwait(false))
        {
            Check.Equal(response.Status.Code, 200, "the status through the blocking bridge");
        }

        blocking.Dispose();
        Check.Equal(transport.Disposals, 0, "the transports disposed when the blocking bridge over them was disposed");

        using var after = await Check.GuardAsync(() => transport.ExecuteAsync(Request.Get(InvariantOptions.Url), token), "a call straight to the transport after everything above was disposed", token).ConfigureAwait(false);
        Check.Equal(after.Status.Code, 200, "the status of the borrowed transport after the components over it were disposed");
    }

    // No candidate or two with no choice is an error naming the problem; an explicit install wins.
    private static Task ResolutionDeterministicAsync(InvariantContext context, CancellationToken token)
    {
        var seam = InvariantContext.RequireHook(context.Subject.SeamResolution, nameof(InvariantSubject.SeamResolution));

        var none = Attempt(seam.ResolveWithNone);
        Check.True(none is not null && none.Message.Length > 0, "resolving a seam with no candidate must fail loudly with an actionable error, not pick nothing silently", "an error with a message", none is null ? "no error" : "an error with no message");

        var two = Attempt(seam.ResolveWithTwo);
        Check.True(two is not null && two.Message.Length > 0, "resolving a seam with two candidates and no explicit selection must fail loudly, not pick one silently", "an error with a message", two is null ? "no error" : "an error with no message");

        Check.True(seam.ExplicitWins(), "an explicit install must win against a discoverable candidate", "the explicit candidate", "another candidate");
        return Task.CompletedTask;
    }

    // A preview is byte-capped, carries the start of the body and does not pull the body ahead of its reader; the reader still sees every byte. An error snapshot is byte-capped, non-empty and a prefix of the body.
    private static async Task PreviewCappedNonConsumingAsync(InvariantContext context, CancellationToken token)
    {
        PatternStream? source = null;
        var transport = context.Transport(ProbeTransport.Behavior.Of((request, _) =>
        {
            source = new PatternStream(LargeBody);
            return new Response(
                request,
                Status.Ok,
                Protocol.Http11,
                new Headers.Builder().Add("Content-Type", "text/plain").Build(),
                ResponseBody.FromStream(source, MediaType.Parse("text/plain"), LargeBody),
                null);
        }));
        var options = new DexpaceClientOptions { Retry = InvariantOptions.Fast(0), Logging = new HttpLoggingOptions { Level = HttpLogLevel.Body, BodyPreviewSize = PreviewCap } };
        var pipeline = context.Build(transport, options);

        using var response = await Check.GuardAsync(() => context.SendAsync(pipeline, Request.Get(InvariantOptions.Url), token), "a call with a 10 MiB response logged at the Body level", token).ConfigureAwait(false);

        Check.True(
            source!.BytesPulled <= PreviewCap + ReadAhead,
            "a body preview must not materialize the payload before its reader asks for it",
            $"at most {PreviewCap + ReadAhead} bytes pulled before the first read",
            source.BytesPulled.ToString(CultureInfo.InvariantCulture));
        var stream = await response.Body.OpenReadAsync(token).ConfigureAwait(false);
        await using var streamScope = stream.ConfigureAwait(false);
        var seen = await ReadAndVerifyAsync(stream, token).ConfigureAwait(false);
        Check.Equal(seen, LargeBody, "the bytes the consumer read after a preview was taken (a preview does not disturb the primary read path)");

        // The preview is in the log: some line carries the start of the payload. A pipeline that takes no preview has none to cap.
        if (!context.Logger.Entries.Any(entry => entry.Contains(PatternStream.Prefix, StringComparison.Ordinal)))
        {
            throw new ConformanceVacuousException("the pipeline logged no body preview at the Body level, so there was no preview to cap");
        }

        var largest = context.Logger.Entries.Select(entry => entry.Message.Length).DefaultIfEmpty(0).Max();
        Check.True(largest <= (PreviewCap * 8) + 4096, "a logged body preview must be byte-capped", $"at most {(PreviewCap * 8) + 4096} characters in any log line", largest.ToString(CultureInfo.InvariantCulture));

        await ErrorSnapshotAsync(context, token).ConfigureAwait(false);
    }

    private static async Task ErrorSnapshotAsync(InvariantContext context, CancellationToken token)
    {
        PatternStream? failing = null;
        var erroring = context.Transport(ProbeTransport.Behavior.Of((request, _) =>
        {
            failing = new PatternStream(LargeBody);
            return new Response(request, Status.FromCode(500), Protocol.Http11, null, ResponseBody.FromStream(failing, MediaType.Parse("text/plain"), LargeBody), null);
        }));
        var mapped = InvariantContext.WithErrorMapping(context.Build(erroring, InvariantOptions.Retrying(0)));
        var snapshot = await context.FailureOfAsync(mapped, Request.Get(InvariantOptions.Url), "a call answered with a 500 carrying a 10 MiB body", token).ConfigureAwait(false);
        var http = snapshot as HttpResponseException
            ?? throw new ConformanceException("an error status must surface as the response-carrying error", "HttpResponseException", Outcome.Name(snapshot));
        Check.True(
            failing!.BytesPulled <= Response.MaxBufferedErrorBytes + ReadAhead,
            "an error-body snapshot must be byte-capped",
            $"at most {Response.MaxBufferedErrorBytes + ReadAhead} bytes pulled",
            failing.BytesPulled.ToString(CultureInfo.InvariantCulture));
        var kept = await Check.GuardAsync(() => http.Response.Body.ReadAsBytesAsync(token), "reading the error's snapshot", token).ConfigureAwait(false);
        Check.True(kept.Length <= Response.MaxBufferedErrorBytes, "the error's snapshot must be byte-capped", $"at most {Response.MaxBufferedErrorBytes} bytes", kept.Length.ToString(CultureInfo.InvariantCulture));
        Check.True(kept.Length > 0, "the error's snapshot must carry the truncated body, not an empty one", "the start of the body", "no bytes");
        for (var i = 0; i < kept.Length; i++)
        {
            if (kept[i] != PatternStream.At(i))
            {
                Check.Fail("the error's snapshot must be a prefix of the body the server sent", $"byte {i} = {PatternStream.At(i)}", kept[i].ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static async Task<long> ReadAndVerifyAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[ReadAhead];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] != PatternStream.At(total + i))
                {
                    Check.Fail("the consumer must read the body unchanged after a preview was taken", $"byte {total + i} = {PatternStream.At(total + i)}", buffer[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            total += read;
        }

        return total;
    }

    // The same call from the thread pool, a dedicated thread, an exclusive scheduler and a single-threaded context completes.
    private static async Task SchedulerAgnosticAsync(InvariantContext context, CancellationToken token)
    {
        var transport = context.Transport(ProbeTransport.Behavior.Status(200));
        var pipeline = context.Build(transport, InvariantOptions.Retrying(0));
        var request = Request.Get(InvariantOptions.Url);
        var exclusive = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
        using var singleThread = new SingleThreadContext();

        var environments = new (string Name, Func<Task<int>> Run)[]
        {
            ("the thread pool", () => Task.Run(() => CallAsync(context, pipeline, request, token), CancellationToken.None)),
            ("a dedicated thread", () => Task.Factory.StartNew(() => CallAsync(context, pipeline, request, token), CancellationToken.None, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap()),
            ("an exclusive scheduler", () => Task.Factory.StartNew(() => CallAsync(context, pipeline, request, token), CancellationToken.None, TaskCreationOptions.DenyChildAttach, exclusive).Unwrap()),
            ("a single-threaded synchronization context", () => singleThread.RunAsync(() => CallAsync(context, pipeline, request, token))),
        };

        foreach (var (name, run) in environments)
        {
            var statuses = await Check.GuardAsync(
                () => Bounded.WaitForAllAsync([run()], context.Bound, $"a call made from {name}", token),
                $"a call made from {name}",
                token).ConfigureAwait(false);
            Check.Equal(statuses[0], 200, $"the status of a call made from {name}");
        }
    }

    // The call runs where the environment put it: the blocking face blocks that thread, the asynchronous face awaits there.
    private static async Task<int> CallAsync(InvariantContext context, HttpPipeline pipeline, Request request, CancellationToken token)
    {
        if (context.Face == TransportFace.Blocking)
        {
            using var blocked = pipeline.Send(request, RequestOptions.Empty, token);
            return blocked.Status.Code;
        }

        using var response = await pipeline.SendAsync(request, RequestOptions.Empty, token).ConfigureAwait(false);
        return response.Status.Code;
    }

    private static Exception? Attempt(Action action)
    {
        try
        {
            action();
            return null;
        }
#pragma warning disable CA1031 // The point of the method: whatever the resolution raises is the answer.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ex;
        }
    }
}
