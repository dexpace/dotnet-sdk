// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The two HTTP client instruments, with the stable attribute sets of the OpenTelemetry HTTP client conventions
/// (OBS-31, OBS-32, OBS-33; design 5c position F, P5c-10).
/// </summary>
/// <remarks>
/// <para>
/// Every tag list is built only when its instrument has a listener, so an unmetered call allocates nothing. The
/// instruments are shared statics over <see cref="DexpaceDiagnostics.Meter"/>. Calls into the instruments are never
/// wrapped: a throwing <see cref="MeterListener"/> callback propagates (OBS-20, design §11 item 37).
/// </para>
/// <para>
/// There is no count instrument (the histogram's count is the request count) and no operation-level instrument.
/// </para>
/// </remarks>
internal static class HttpClientMetrics
{
    // OpenTelemetry's recommended bucket boundaries for http.client.request.duration, in seconds.
    private static readonly double[] s_bucketBoundaries =
        [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    /// <summary><c>http.client.request.duration</c>, in seconds.</summary>
    internal static Histogram<double> RequestDuration { get; } =
        DexpaceDiagnostics.Meter.CreateHistogram(
            "http.client.request.duration",
            unit: "s",
            description: "Duration of HTTP client requests.",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_bucketBoundaries });

    /// <summary><c>http.client.active_requests</c>, in requests.</summary>
    internal static UpDownCounter<long> ActiveRequests { get; } =
        DexpaceDiagnostics.Meter.CreateUpDownCounter<long>(
            "http.client.active_requests",
            unit: "{request}",
            description: "Number of HTTP requests currently in flight.");

    /// <summary>Counts <paramref name="request"/> in flight when the instrument has a listener.</summary>
    /// <param name="request">The request being sent.</param>
    /// <returns>
    /// Whether the increment was recorded: only then may <see cref="RequestEnded"/> decrement, so a listener that appears
    /// mid-call never sees a decrement it did not see the increment for (OBS-33).
    /// </returns>
    internal static bool RequestStarted(Request request)
    {
        if (!ActiveRequests.Enabled)
        {
            return false;
        }

        var tags = default(TagList);
        AppendStartTags(ref tags, request);
        ActiveRequests.Add(1, tags);
        return true;
    }

    /// <summary>Counts <paramref name="request"/> out of flight when <see cref="RequestStarted"/> counted it in.</summary>
    /// <param name="request">The request that was counted in.</param>
    /// <param name="counted">The result of <see cref="RequestStarted"/>.</param>
    internal static void RequestEnded(Request request, bool counted)
    {
        if (!counted)
        {
            return;
        }

        var tags = default(TagList);
        AppendStartTags(ref tags, request);
        ActiveRequests.Add(-1, tags);
    }

    /// <summary>Records the duration of one attempt.</summary>
    /// <param name="request">The request the attempt sent.</param>
    /// <param name="seconds">The elapsed time in seconds.</param>
    /// <param name="response">The response, or <see langword="null"/> when the attempt failed.</param>
    /// <param name="failure">The failure, or <see langword="null"/> when a response came back.</param>
    internal static void RecordDuration(Request request, double seconds, Response? response, Exception? failure)
    {
        if (!RequestDuration.Enabled)
        {
            return;
        }

        var tags = default(TagList);
        AppendStartTags(ref tags, request);
        AppendOutcomeTags(ref tags, response, failure);
        RequestDuration.Record(seconds, tags);
    }

    private static void AppendStartTags(ref TagList tags, Request request)
    {
        var url = request.Url;
        tags.Add(HttpSemanticConventions.RequestMethod, HttpSemanticConventions.MethodValue(request.Method));
        tags.Add(HttpSemanticConventions.ServerAddress, url.Host);
        tags.Add(HttpSemanticConventions.ServerPort, url.Port);
        tags.Add(HttpSemanticConventions.UrlScheme, url.Scheme);
    }

    private static void AppendOutcomeTags(ref TagList tags, Response? response, Exception? failure)
    {
        if (response is not null)
        {
            var code = response.Status.Code;
            tags.Add(HttpSemanticConventions.ResponseStatusCode, HttpSemanticConventions.BoxedStatusCode(code));
            if (HttpSemanticConventions.ProtocolVersion(response.Protocol) is { } version)
            {
                tags.Add(HttpSemanticConventions.NetworkProtocolVersion, version);
            }

            if (code >= 400)
            {
                tags.Add(HttpSemanticConventions.ErrorTypeKey, HttpSemanticConventions.ErrorType(code));
            }
        }
        else if (failure is not null)
        {
            tags.Add(HttpSemanticConventions.ErrorTypeKey, HttpSemanticConventions.ErrorType(failure));
        }
    }
}
