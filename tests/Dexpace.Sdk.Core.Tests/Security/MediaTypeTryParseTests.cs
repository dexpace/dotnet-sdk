// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S9's model half (TRANSPORT-27; design §4.4): <see cref="MediaType.TryParse"/> is the lenient,
/// non-throwing parse a transport uses for an inbound <c>Content-Type</c>, so an unparseable value becomes "no media
/// type" instead of an exception. The wire-level proof is
/// <c>Dexpace.Sdk.Http.SystemNet.Tests.Security.MalformedContentTypeWireTests</c>. Permanent (roadmap constraint 5).
/// </summary>
[Trait("Category", "Security")]
public sealed class MediaTypeTryParseTests
{
    [Theory]
    [InlineData("text/plain; foo")]
    [InlineData("text")]
    [InlineData("text/plain/extra")]
    [InlineData("")]
    [InlineData("te xt/plain")]
    [InlineData("*/json")]
    [InlineData("text/plain; q=\"a\r\nb\"")]
    [InlineData("text/plain; name=vålue")]
    [InlineData(null)]
    public void An_unparseable_value_yields_false_and_no_media_type(string? value)
    {
        Assert.False(MediaType.TryParse(value, out var mediaType));
        Assert.Null(mediaType);
    }

    [Theory]
    [InlineData("text/plain; charset=utf-8", "text/plain")]
    [InlineData("Application/JSON", "application/json")]
    [InlineData("multipart/form-data; boundary=\"a;b\"", "multipart/form-data")]
    public void A_well_formed_value_parses_exactly_as_parse_does(string value, string fullType)
    {
        Assert.True(MediaType.TryParse(value, out var mediaType));
        Assert.Equal(fullType, mediaType.FullType);
        Assert.Equal(MediaType.Parse(value), mediaType);
    }
}
