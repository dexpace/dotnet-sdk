// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-39, OBS-3, OBS-4: the published log vocabulary.</summary>
[Trait("Category", "Unit")]
public sealed class LogVocabularyTests
{
    private static IEnumerable<FieldInfo> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral);

    private static IEnumerable<string> StringConstants(Type type) =>
        Constants(type).Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Event_names_and_ids_are_the_published_values()
    {
        Assert.Equal("http.request", DexpaceLogEvents.HttpRequest);
        Assert.Equal("http.response", DexpaceLogEvents.HttpResponse);
        Assert.Equal("http.instrumentation.log_failed", DexpaceLogEvents.LogFailed);
        Assert.Equal("http.instrumentation.body_capture_failed", DexpaceLogEvents.BodyCaptureFailed);
        Assert.Equal("dexpace.dispose.suppressed", DexpaceLogEvents.DisposeSuppressed);
        Assert.Equal(100, DexpaceLogEvents.HttpRequestId);
        Assert.Equal(101, DexpaceLogEvents.HttpResponseId);
        Assert.Equal(102, DexpaceLogEvents.HttpFailureId);
        Assert.Equal(120, DexpaceLogEvents.LogFailedId);
        Assert.Equal(121, DexpaceLogEvents.BodyCaptureFailedId);
        Assert.Equal(130, DexpaceLogEvents.DisposeSuppressedId);
    }

    [Fact]
    public void No_key_or_event_name_is_empty_or_collides()
    {
        var all = StringConstants(typeof(DexpaceLogEvents)).Concat(StringConstants(typeof(DexpaceLogKeys))).ToList();

        Assert.All(all, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_header_prefixes_end_with_a_dot_and_no_key_is_named_event()
    {
        Assert.EndsWith(".", DexpaceLogKeys.HttpRequestHeaderPrefix, StringComparison.Ordinal);
        Assert.EndsWith(".", DexpaceLogKeys.HttpResponseHeaderPrefix, StringComparison.Ordinal);
        Assert.DoesNotContain(
            StringConstants(typeof(DexpaceLogEvents)).Concat(StringConstants(typeof(DexpaceLogKeys))),
            value => string.Equals(value, "event", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Keys_are_the_OpenTelemetry_names()
    {
        string[] expected =
        [
            "http.request.method", "url.full", "http.request.resend_count", "http.response.status_code",
            "http.response.duration_ms", "error.type", "http.request.body.size", "http.response.body.size",
            "http.request.body.preview", "http.response.body.preview", "http.request.body.preview.size",
            "http.response.body.preview.size", "http.request.header.", "http.response.header.", "REDACTED",
            "dexpace.instrumentation.failed_event",
        ];

        Assert.Equal(16, StringConstants(typeof(DexpaceLogKeys)).Count());
        Assert.Equal(expected.Order(StringComparer.Ordinal), StringConstants(typeof(DexpaceLogKeys)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Id_ranges_are_inside_the_reserved_blocks()
    {
        var ids = Constants(typeof(DexpaceLogEvents)).Where(f => f.FieldType == typeof(int))
            .ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);

        foreach (var name in (string[])["HttpRequestId", "HttpResponseId", "HttpFailureId"])
        {
            Assert.InRange(ids[name], 100, 109);
        }

        foreach (var name in (string[])["LogFailedId", "BodyCaptureFailedId"])
        {
            Assert.InRange(ids[name], 120, 129);
        }

        Assert.InRange(ids["DisposeSuppressedId"], 130, 139);

        // 6a (P6a-28): 140 is the only id in the retry block; 141-149 stay reserved. 6b and 6c each edit their own block.
        Assert.Equal(140, ids["RetryDelayOverrideFailedId"]);

        // 110-119 (8b header drops), 141-149 (retry), 150-159 (redirect) and 160-169 (auth) are reserved and unused.
        Assert.DoesNotContain(ids.Values, id => id is (>= 110 and <= 119) or (>= 141 and <= 169));
        Assert.Equal(ids.Count, ids.Values.Distinct().Count());
    }

    [Fact]
    public void The_internal_dispose_key_is_the_published_literal_and_collides_with_nothing()
    {
        Assert.Equal("dexpace.dispose.resource_type", InternalLogKeys.DisposeResourceType);
        Assert.DoesNotContain(
            StringConstants(typeof(DexpaceLogKeys)).Concat(StringConstants(typeof(DexpaceLogEvents))),
            value => value == InternalLogKeys.DisposeResourceType);
    }
}
