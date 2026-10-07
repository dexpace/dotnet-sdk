// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>The pure mappings of the HTTP client conventions (P5c-8, P5c-9).</summary>
[Trait("Category", "Unit")]
public sealed class HttpSemanticConventionsTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("CONNECT")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("PATCH")]
    public void MethodValue_is_the_method_for_the_nine_known_methods(string method)
    {
        Assert.Equal(method, HttpSemanticConventions.MethodValue(Method.Of(method)));
        Assert.Equal(method, HttpSemanticConventions.SpanName(Method.Of(method)));
        Assert.False(HttpSemanticConventions.IsOther(Method.Of(method)));
    }

    [Theory]
    [InlineData("PURGE")]
    [InlineData("MKCOL2")]
    public void MethodValue_is_OTHER_and_the_span_is_HTTP_for_any_other_token(string method)
    {
        Assert.Equal("_OTHER", HttpSemanticConventions.MethodValue(Method.Of(method)));
        Assert.Equal("HTTP", HttpSemanticConventions.SpanName(Method.Of(method)));
        Assert.True(HttpSemanticConventions.IsOther(Method.Of(method)));
    }

    [Fact]
    public void A_lower_case_known_method_is_normalised_by_Method_Of()
    {
        Assert.Equal("GET", HttpSemanticConventions.MethodValue(Method.Of("get")));
    }

    [Fact]
    public void ErrorType_of_a_status_is_the_cached_decimal_string_for_400_to_599()
    {
        Assert.Equal("404", HttpSemanticConventions.ErrorType(404));
        Assert.Same(HttpSemanticConventions.ErrorType(503), HttpSemanticConventions.ErrorType(503));
        Assert.Equal("399", HttpSemanticConventions.ErrorType(399));
        Assert.Equal("600", HttpSemanticConventions.ErrorType(600));
    }

    [Fact]
    public void ErrorType_of_an_exception_is_its_full_type_name()
    {
        Assert.Equal("System.InvalidOperationException", HttpSemanticConventions.ErrorType(new InvalidOperationException()));
        Assert.Contains("NestedException", HttpSemanticConventions.ErrorType(new NestedException()), StringComparison.Ordinal);
    }

    [Fact]
    public void BoxedStatusCode_returns_the_same_boxed_instance_for_100_to_599()
    {
        Assert.Same(HttpSemanticConventions.BoxedStatusCode(200), HttpSemanticConventions.BoxedStatusCode(200));
        Assert.Equal(503, HttpSemanticConventions.BoxedStatusCode(503));
        Assert.NotSame(HttpSemanticConventions.BoxedStatusCode(99), HttpSemanticConventions.BoxedStatusCode(99));
        Assert.Equal(99, HttpSemanticConventions.BoxedStatusCode(99));
    }

    [Fact]
    public void ProtocolVersion_maps_every_Protocol_member()
    {
        Assert.Equal("1.0", HttpSemanticConventions.ProtocolVersion(Protocol.Http10));
        Assert.Equal("1.1", HttpSemanticConventions.ProtocolVersion(Protocol.Http11));
        Assert.Equal("2", HttpSemanticConventions.ProtocolVersion(Protocol.Http2));
        Assert.Equal("2", HttpSemanticConventions.ProtocolVersion(Protocol.H2PriorKnowledge));
        Assert.Equal("3", HttpSemanticConventions.ProtocolVersion(Protocol.Quic));
        Assert.Null(HttpSemanticConventions.ProtocolVersion((Protocol)99));
    }

    [Fact]
    public void The_attribute_name_constants_are_the_stable_convention_keys()
    {
        Assert.Equal("http.request.method", HttpSemanticConventions.RequestMethod);
        Assert.Equal("http.request.method_original", HttpSemanticConventions.RequestMethodOriginal);
        Assert.Equal("http.request.resend_count", HttpSemanticConventions.RequestResendCount);
        Assert.Equal("http.response.status_code", HttpSemanticConventions.ResponseStatusCode);
        Assert.Equal("server.address", HttpSemanticConventions.ServerAddress);
        Assert.Equal("server.port", HttpSemanticConventions.ServerPort);
        Assert.Equal("url.scheme", HttpSemanticConventions.UrlScheme);
        Assert.Equal("url.full", HttpSemanticConventions.UrlFull);
        Assert.Equal("network.protocol.version", HttpSemanticConventions.NetworkProtocolVersion);
        Assert.Equal("error.type", HttpSemanticConventions.ErrorTypeKey);
        Assert.Equal("dexpace.attempt.failed", HttpSemanticConventions.AttemptFailedEvent);
        Assert.Equal("dexpace.retry.exhausted", HttpSemanticConventions.RetryExhaustedEvent);
        Assert.Equal("dexpace.redirect.hop", HttpSemanticConventions.RedirectHopEvent);
        Assert.Equal("dexpace.retry.delay", HttpSemanticConventions.RetryDelay);
        Assert.Equal("dexpace.retry.attempts", HttpSemanticConventions.RetryAttempts);
        Assert.Equal("dexpace.redirect.hop", HttpSemanticConventions.RedirectHop);
        Assert.Equal("dexpace.redirect.cross_origin", HttpSemanticConventions.RedirectCrossOrigin);
    }

    private sealed class NestedException : Exception;
}
