// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>HTTP-50 (and HTTP-3 for RequestConditions), with the P2a-3 ruling.</summary>
[Trait("Category", "Unit")]
public class RequestConditionsTests
{
    [Fact]
    public void None_has_nothing_set()
    {
        Assert.True(RequestConditions.None.IfMatch.IsEmpty);
        Assert.True(RequestConditions.None.IfNoneMatch.IsEmpty);
        Assert.Null(RequestConditions.None.IfModifiedSince);
        Assert.Null(RequestConditions.None.IfUnmodifiedSince);
        Assert.Equal(RequestConditions.None, new RequestConditions());
        Assert.Same(Headers.Empty, RequestConditions.None.ApplyTo(Headers.Empty));
    }

    [Fact]
    public void A_default_ImmutableArray_is_normalised_to_empty()
    {
        var conditions = new RequestConditions { IfMatch = default };

        Assert.False(conditions.IfMatch.IsDefault);
        Assert.True(conditions.IfMatch.IsEmpty);
        Assert.Equal(RequestConditions.None, conditions);
    }

    [Fact]
    public void ApplyTo_joins_each_list_into_one_header_with_commas()
    {
        var conditions = new RequestConditions
        {
            IfMatch = [ETag.Strong("a"), ETag.Strong("b")],
            IfNoneMatch = [ETag.Weak("c"), ETag.Strong("d")],
        };

        var headers = conditions.ApplyTo(Headers.Empty);

        Assert.Equal("\"a\", \"b\"", headers.Get("If-Match"));
        Assert.Equal((string[])["\"a\", \"b\""], headers.GetAll("If-Match"));
        Assert.Equal("W/\"c\", \"d\"", headers.Get(HttpHeaderName.WellKnown.IfNoneMatch));
    }

    [Fact]
    public void A_single_star_renders_as_star()
    {
        var headers = new RequestConditions { IfMatch = [ETag.Any] }.ApplyTo(Headers.Empty);

        Assert.Equal("*", headers.Get("If-Match"));
    }

    [Fact]
    public void Dates_render_in_R_format_converted_to_UTC()
    {
        var conditions = new RequestConditions
        {
            IfModifiedSince = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.FromHours(3)),
            IfUnmodifiedSince = new DateTimeOffset(2026, 6, 14, 23, 30, 5, TimeSpan.FromHours(-5)),
        };

        var headers = conditions.ApplyTo(Headers.Empty);

        Assert.Equal("Sun, 14 Jun 2026 09:00:00 GMT", headers.Get("If-Modified-Since"));
        Assert.Equal("Mon, 15 Jun 2026 04:30:05 GMT", headers.Get("If-Unmodified-Since"));
    }

    [Fact]
    public void Applying_twice_gives_equal_headers()
    {
        // Idempotent: ApplyTo uses Set, never With.
        var conditions = new RequestConditions
        {
            IfMatch = [ETag.Strong("a")],
            IfModifiedSince = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var once = conditions.ApplyTo(Headers.Empty);
        var twice = conditions.ApplyTo(once);

        Assert.Equal(once, twice);
        Assert.Equal((string[])["\"a\""], twice.GetAll("If-Match"));
    }

    [Fact]
    public void Unset_members_leave_existing_headers_untouched()
    {
        var existing = Headers.Empty.With("If-Match", "\"keep\"").With("X-Other", "1");

        var applied = new RequestConditions { IfNoneMatch = [ETag.Strong("n")] }.ApplyTo(existing);

        Assert.Equal("\"keep\"", applied.Get("If-Match"));
        Assert.Equal("1", applied.Get("X-Other"));
        Assert.Equal("\"n\"", applied.Get("If-None-Match"));
    }

    [Fact]
    public void Star_is_exclusive_with_concrete_tags()
    {
        ImmutableArray<ETag> mixed = [ETag.Any, ETag.Strong("a")];
        ImmutableArray<ETag> mixedOther = [ETag.Strong("a"), ETag.Any];

        Assert.Throws<ArgumentException>(() => new RequestConditions { IfMatch = mixed });
        Assert.Throws<ArgumentException>(() => new RequestConditions { IfNoneMatch = mixedOther });
        Assert.Throws<ArgumentException>(() => RequestConditions.None with { IfMatch = mixed });
        Assert.Throws<ArgumentException>(() => RequestConditions.None with { IfNoneMatch = mixedOther });
    }

    [Fact]
    public void A_repeated_star_collapses()
    {
        var conditions = new RequestConditions { IfMatch = [ETag.Any, ETag.Any] };

        Assert.Equal([ETag.Any], conditions.IfMatch);
    }

    [Fact]
    public void Equality_is_sequence_equality_over_the_arrays()
    {
        var a = new RequestConditions { IfMatch = [ETag.Strong("a"), ETag.Strong("b")] };
        var b = new RequestConditions { IfMatch = [ETag.Strong("a"), ETag.Strong("b")] };

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new RequestConditions { IfMatch = [ETag.Strong("b"), ETag.Strong("a")] });
        Assert.NotEqual(a, new RequestConditions { IfNoneMatch = [ETag.Strong("a"), ETag.Strong("b")] });
        Assert.NotEqual(a, a with { IfModifiedSince = DateTimeOffset.UnixEpoch });
        Assert.Equal(
            new RequestConditions { IfModifiedSince = DateTimeOffset.UnixEpoch },
            new RequestConditions { IfModifiedSince = DateTimeOffset.UnixEpoch });
        Assert.False(a.Equals((RequestConditions?)null));
    }

    [Fact]
    public void ApplyTo_Request_returns_a_request_with_the_headers_and_leaves_the_original_unchanged()
    {
        var request = Request.Get("https://example.test/x").WithHeader("X-Keep", "1");
        var conditions = new RequestConditions { IfNoneMatch = [ETag.Strong("v1")] };

        var conditional = conditions.ApplyTo(request);

        Assert.Equal("\"v1\"", conditional.Headers.Get("If-None-Match"));
        Assert.Equal("1", conditional.Headers.Get("X-Keep"));
        Assert.False(request.Headers.Contains("If-None-Match"));
        Assert.Equal(request.Url, conditional.Url);
        Assert.Throws<ArgumentNullException>(() => conditions.ApplyTo((Request)null!));
        Assert.Throws<ArgumentNullException>(() => conditions.ApplyTo((Headers)null!));
    }

    [Fact]
    public void A_with_derivation_leaves_the_original_unchanged()
    {
        // HTTP-3 for this type.
        var original = new RequestConditions
        {
            IfMatch = [ETag.Strong("a")],
            IfModifiedSince = DateTimeOffset.UnixEpoch,
        };

        var derived = original with { IfMatch = [ETag.Strong("z"), ETag.Strong("y")], IfModifiedSince = null };

        Assert.Equal([ETag.Strong("a")], original.IfMatch);
        Assert.Equal(DateTimeOffset.UnixEpoch, original.IfModifiedSince);
        Assert.Equal(2, derived.IfMatch.Length);
        Assert.Null(derived.IfModifiedSince);
    }

    [Fact]
    public void A_non_ASCII_ETag_is_valid_but_ApplyTo_cannot_send_it()
    {
        // RULED (P2a-3): obs-text is valid under HTTP-48, and HTTP-18's outbound rule wins at ApplyTo.
        var tag = ETag.Strong("é");
        var conditions = new RequestConditions { IfMatch = [tag] };

        var error = Assert.Throws<ArgumentException>(() => conditions.ApplyTo(Headers.Empty));

        Assert.DoesNotContain("é", error.Message, StringComparison.Ordinal);
        Assert.Contains("U+00E9", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestConditions_is_a_sealed_record_whose_parameterless_constructor_is_the_only_one()
    {
        Assert.True(typeof(RequestConditions).IsSealed);
        Assert.Single(typeof(RequestConditions).GetConstructors());
    }
}
