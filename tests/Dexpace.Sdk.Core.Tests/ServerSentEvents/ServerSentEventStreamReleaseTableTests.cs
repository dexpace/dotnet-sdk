// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>
/// The release table of P7b-12 as one pin: for each path, does a release failure propagate, is it reported out of band
/// (the activity event and the id-130 log event, together), and is it attached to a primary exception (SSE-24, SSE-25,
/// SSE-28, SSE-29, SSE-30, SSE-34, SSE-36).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServerSentEventStreamReleaseTableTests : IDisposable
{
    private readonly ActivitySource _source = new("Dexpace.Sdk.Core.Tests.ServerSentEventStreamReleaseTable");
    private readonly ActivityListener _listener;

    public ServerSentEventStreamReleaseTableTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == _source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    // path, the release failure propagates, it is reported out of band, it is attached to a primary exception
    public static TheoryData<string, bool, bool, bool> Paths => new()
    {
        { "natural-end", false, true, false },
        { "early-enumerator-dispose", false, true, false },
        { "blocking-early-enumerator-dispose", false, true, false },
        { "mid-stream-failure", false, false, true },
        { "blocking-mid-stream-failure", false, false, true },
        { "explicit-dispose", true, false, false },
        { "explicit-dispose-async", true, false, false },
        { "call-after-first-release", false, false, false },
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Each_path_propagates_reports_or_attaches_exactly_as_the_table_says(
        string path,
        bool propagates,
        bool reported,
        bool attached)
    {
        var releaseFailure = new InvalidOperationException("release boom");
        var logger = new RecordingLogger();
        using var activity = _source.StartActivity("release-table");
        Assert.NotNull(activity);
        var failing = path.EndsWith("mid-stream-failure", StringComparison.Ordinal);
        var body = new SseBody(failing
            ? () => new FailingAfterStream("data: a\n\n"u8.ToArray(), new IOException("dropped"))
            : SseResponses.Bytes("data: a\n\ndata: b\n\n"))
        {
            DisposeFailure = releaseFailure,
        };
        var stream = ServerSentEventStream.FromResponse(SseResponses.Respond(body), logger: logger);

        var primary = await Drive(path, stream);

        Assert.Equal(1, body.DisposeCount);
        var thrownIsReleaseFailure = ReferenceEquals(primary, releaseFailure);
        Assert.Equal(propagates, thrownIsReleaseFailure);
        var activityReported = activity.Events.Any(e => e.Name == "exception" && e.Tags.Any(t => t.Key == "dexpace.dispose.suppressed"));
        var loggerReported = logger.Entries.Any(e => e.EventId.Id == DexpaceLogEvents.DisposeSuppressedId);
        Assert.Equal(reported, activityReported);
        Assert.Equal(reported, loggerReported);
        var isAttached = primary is not null && !thrownIsReleaseFailure && ExceptionTrail.GetSuppressed(primary).Contains(releaseFailure);
        Assert.Equal(attached, isAttached);
    }

    // Runs one path and returns the exception that reached the caller, or null when none did.
    private static async Task<Exception?> Drive(string path, ServerSentEventStream stream)
    {
        try
        {
            switch (path)
            {
                case "natural-end":
                    await foreach (var unused in stream.WithCancellation(Token))
                    {
                    }

                    break;
                case "early-enumerator-dispose":
                    await foreach (var unused in stream.WithCancellation(Token))
                    {
                        break;
                    }

                    break;
                case "blocking-early-enumerator-dispose":
                    foreach (var unused in stream.AsEnumerable())
                    {
                        break;
                    }

                    break;
                case "mid-stream-failure":
                    await foreach (var unused in stream.WithCancellation(Token))
                    {
                    }

                    break;
                case "blocking-mid-stream-failure":
                    foreach (var unused in stream.AsEnumerable())
                    {
                    }

                    break;
                case "explicit-dispose":
                    stream.Dispose();
                    break;
                case "explicit-dispose-async":
                    await stream.DisposeAsync();
                    break;
                case "call-after-first-release":
                    return await DriveCallAfterFirstRelease(stream);
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown release path.");
            }

            return null;
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            return ex;
        }
    }

    // The first release propagates (it is the explicit one); every later call, in any mix, does nothing at all.
    private static async Task<Exception?> DriveCallAfterFirstRelease(ServerSentEventStream stream)
    {
        Exception? first = null;
        try
        {
            stream.Dispose();
        }
        catch (InvalidOperationException ex)
        {
            first = ex;
        }

        Assert.NotNull(first);
        stream.Dispose();
        await stream.DisposeAsync();
        stream.Dispose();

        // The table row is about the later calls: report that none of them threw by returning null.
        return null;
    }
}
