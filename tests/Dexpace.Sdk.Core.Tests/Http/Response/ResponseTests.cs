// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// The phase 2a rows for <see cref="Response"/>: HTTP-2, HTTP-3, HTTP-4, HTTP-6, HTTP-11 (Response half) and SEAM-29
/// (the Response half).
/// </summary>
[Trait("Category", "Unit")]
public class ResponseTests
{
    // code, informational, success, redirect, client error, server error, error
    public static TheoryData<int, bool, bool, bool, bool, bool, bool> Boundaries => new()
    {
        { 99, false, false, false, false, false, false },
        { 100, true, false, false, false, false, false },
        { 199, true, false, false, false, false, false },
        { 200, false, true, false, false, false, false },
        { 299, false, true, false, false, false, false },
        { 300, false, false, true, false, false, false },
        { 399, false, false, true, false, false, false },
        { 400, false, false, false, true, false, true },
        { 499, false, false, false, true, false, true },
        { 500, false, false, false, false, true, true },
        { 599, false, false, false, false, true, true },
        { 600, false, false, false, false, false, false },
    };

    private static Request SomeRequest => Request.Get("https://example.test/items");

    // ---- Constructor (7.2) ----

    [Fact]
    public void A_missing_request_throws_ArgumentNullException_with_ParamName_request()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new Response(null!, Status.Ok, Protocol.Http11));

        Assert.Equal("request", error.ParamName);
    }

    [Fact]
    public void An_undefined_protocol_throws_ArgumentOutOfRangeException_with_ParamName_protocol()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Response(SomeRequest, Status.Ok, (Protocol)999));

        Assert.Equal("protocol", error.ParamName);
    }

    [Fact]
    public void The_protocol_parameter_has_no_default_value()
    {
        // The silent Http11 default is gone (HTTP-4): a compile-time rule, asserted here by reflection.
        var constructor = Assert.Single(typeof(Response).GetConstructors());
        var parameters = constructor.GetParameters().ToDictionary(p => p.Name!);

        Assert.False(parameters["protocol"].HasDefaultValue);
        Assert.False(parameters["request"].HasDefaultValue);
        Assert.True(parameters["headers"].HasDefaultValue);
        Assert.True(parameters["body"].HasDefaultValue);
        Assert.True(parameters["reasonPhrase"].HasDefaultValue);
    }

    [Fact]
    public void Status_is_a_required_positional_parameter()
    {
        var constructor = Assert.Single(typeof(Response).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(["request", "status", "protocol", "headers", "body", "reasonPhrase"], parameters.Select(p => p.Name));
        Assert.False(parameters[1].HasDefaultValue);
        Assert.Equal(typeof(Status), parameters[1].ParameterType);
    }

    [Fact]
    public void Request_and_reason_phrase_round_trip()
    {
        var request = SomeRequest;

        using var response = new Response(request, Status.Ok, Protocol.Http2, reasonPhrase: "Fine");

        Assert.Same(request, response.Request);
        Assert.Equal("Fine", response.ReasonPhrase);
        Assert.Equal(Protocol.Http2, response.Protocol);
        Assert.Equal(Status.Ok, response.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(0)]
    [InlineData(127)]
    public void A_reason_phrase_with_a_control_character_throws_ArgumentException(int code)
    {
        var phrase = "Fine" + (char)code + "secret";

        var error = Assert.Throws<ArgumentException>(
            () => new Response(SomeRequest, Status.Ok, Protocol.Http11, reasonPhrase: phrase));

        Assert.Equal("reasonPhrase", error.ParamName);
        Assert.Contains($"U+{code:X4}", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Fine", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', error.Message);
        Assert.DoesNotContain('\n', error.Message);
    }

    [Fact]
    public void A_null_reason_phrase_is_accepted()
    {
        using var response = new Response(SomeRequest, Status.Ok, Protocol.Http11, reasonPhrase: null);

        Assert.Null(response.ReasonPhrase);
    }

    [Fact]
    public void Obs_text_in_a_reason_phrase_is_accepted()
    {
        using var response = new Response(SomeRequest, Status.Ok, Protocol.Http11, reasonPhrase: "Café \t ok");

        Assert.Equal("Café \t ok", response.ReasonPhrase);
    }

    [Fact]
    public void Headers_is_never_null()
    {
        using var response = new Response(SomeRequest, Status.Ok, Protocol.Http11, headers: null);

        Assert.Same(Headers.Empty, response.Headers);
    }

    [Fact]
    public async Task An_absent_body_is_an_empty_buffered_body_never_null()
    {
        // P2a-4: optional at construction, never null on read.
        using var response = new Response(SomeRequest, Status.Ok, Protocol.Http11);

        Assert.NotNull(response.Body);
        Assert.Equal(0, response.Body.ContentLength);
        Assert.Empty(await response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TestResponses_Create_defaults_the_request_to_a_GET_on_example_test()
    {
        using var defaulted = TestResponses.Create(Status.Ok);
        var request = SomeRequest;
        using var explicitRequest = TestResponses.Create(Status.Ok, request, reasonPhrase: "OK");

        Assert.Equal(Method.Get, defaulted.Request.Method);
        Assert.Equal("https://example.test/", defaulted.Request.Url.AbsoluteUri);
        Assert.Same(request, explicitRequest.Request);
        Assert.Equal("OK", explicitRequest.ReasonPhrase);
    }

    // ---- Classification (7.3) ----

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Classification_by_boundary_code(
        int code, bool informational, bool success, bool redirect, bool clientError, bool serverError, bool error)
    {
        using var response = TestResponses.Create(Status.FromCode(code));

        Assert.Equal(informational, response.IsInformational);
        Assert.Equal(success, response.IsSuccess);
        Assert.Equal(redirect, response.IsRedirect);
        Assert.Equal(clientError, response.IsClientError);
        Assert.Equal(serverError, response.IsServerError);
        Assert.Equal(error, response.IsError);
    }

    // ---- WithBody (7.4) ----

    [Fact]
    public void WithBody_returns_a_new_response_owning_the_new_body_and_keeps_the_rest()
    {
        var request = SomeRequest;
        var headers = Headers.Empty.With("X-A", "1");
        using var original = new Response(request, Status.Created, Protocol.Http2, headers, reasonPhrase: "Made");
        var replacement = ResponseBody.FromBytes("new"u8.ToArray());

        using var derived = original.WithBody(replacement);

        Assert.NotSame(original, derived);
        Assert.Same(replacement, derived.Body);
        Assert.Same(request, derived.Request);
        Assert.Equal(Status.Created, derived.Status);
        Assert.Equal(Protocol.Http2, derived.Protocol);
        Assert.Equal("Made", derived.ReasonPhrase);
        Assert.Equal(headers, derived.Headers);
    }

    [Fact]
    public void WithBody_rejects_null()
    {
        using var response = TestResponses.Create(Status.Ok);

        Assert.Throws<ArgumentNullException>(() => response.WithBody(null!));
    }

    [Fact]
    public async Task The_original_keeps_its_own_body_and_can_still_be_disposed()
    {
        var originalBody = new TrackingBody();
        var newBody = new TrackingBody();
        var original = TestResponses.Create(Status.Ok, body: originalBody);
        var derived = original.WithBody(newBody);

        Assert.Same(originalBody, original.Body);
        await original.DisposeAsync();

        Assert.True(originalBody.Disposed);
        Assert.False(newBody.Disposed);
        await derived.DisposeAsync();
        Assert.True(newBody.Disposed);
    }

    [Fact]
    public void There_is_no_WithHeaders()
    {
        // Guards the ownership rule: a WithHeaders would leave two Response objects owning one body, and both would
        // dispose it. WithBody is the only derivation.
        var derivations = typeof(Response)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("With", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(["WithBody"], derivations);
    }

    private sealed class TrackingBody : ResponseBody
    {
        public bool Disposed { get; private set; }

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
