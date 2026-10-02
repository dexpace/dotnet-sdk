// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serdes;

/// <summary>SEAM-19 (design §3.4): the codec declares its own media type, and body creation stamps it.</summary>
[Trait("Category", "Unit")]
public sealed class SerdeSeamTests
{
    private static readonly MediaType s_xml = MediaType.Parse("application/xml");

    [Fact]
    public void DefaultMediaType_has_no_default_implementation()
    {
        var getter = typeof(ISerde).GetProperty("DefaultMediaType")!.GetMethod!;

        Assert.True(getter.IsAbstract);
    }

    [Fact]
    public void RequestBody_FromValue_stamps_the_codecs_own_media_type()
    {
        var body = RequestBody.FromValue("v", new BytesSerde(s_xml, [1, 2]));

        Assert.Equal(s_xml, body.ContentType);
    }

    [Fact]
    public void FromValue_lets_an_explicit_media_type_win()
    {
        var body = RequestBody.FromValue("v", new BytesSerde(s_xml, [1, 2]), CommonMediaTypes.ApplicationJsonUtf8);

        Assert.Equal(CommonMediaTypes.ApplicationJsonUtf8, body.ContentType);
    }
}
