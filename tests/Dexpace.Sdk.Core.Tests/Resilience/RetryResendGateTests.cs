// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Resilience;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

/// <summary>The re-send gate (RETRY-5 to RETRY-8, RECOV-18, XCUT-10, BODY-4, BODY-5; P6a-10).</summary>
[Trait("Category", "Unit")]
public sealed class RetryResendGateTests
{
    [Fact]
    public void IdempotentMethods_is_exactly_GET_HEAD_OPTIONS_PUT_DELETE()
    {
        var names = RetryFacts.IdempotentMethods.Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["DELETE", "GET", "HEAD", "OPTIONS", "PUT"], names);
        foreach (var absent in new[] { Method.Trace, Method.Post, Method.Patch, Method.Connect })
        {
            Assert.DoesNotContain(absent, RetryFacts.IdempotentMethods.AsEnumerable());
        }
    }

    public static IEnumerable<object[]> Methods() =>
        new[] { Method.Get, Method.Put, Method.Delete, Method.Post, Method.Patch }.Select(m => new object[] { m.Name });

    // The replayability of each body is hard-coded here, so a body that misreports IsReplayable fails the matrix.
    public static IEnumerable<(string Name, Func<RequestBody?> Make, bool Replayable)> Bodies()
    {
        yield return ("none", () => null, false);
        yield return ("bytes", () => RequestBody.FromBytes(new byte[] { 1, 2 }), true);
        yield return ("empty bytes", () => RequestBody.FromBytes(ReadOnlyMemory<byte>.Empty), true);
        yield return ("string", () => RequestBody.FromString("x"), true);
        yield return ("form", () => RequestBody.FromForm([new KeyValuePair<string, string>("a", "b")]), true);
        yield return ("file", () => RequestBody.FromFile(typeof(RetryResendGateTests).Assembly.Location), true);
        yield return ("seekable stream with length", () => RequestBody.FromStream(new MemoryStream(new byte[] { 1, 2, 3 }), null, 3), true);
        yield return ("single-use stream", () => RequestBody.FromStream(new NonSeekable(new byte[] { 1, 2, 3 })), false);
        yield return ("multipart, replayable parts", () => RequestBody.Multipart([new MultipartPart("a", RequestBody.FromString("x"))]), true);
        yield return ("multipart, single-use part", () => RequestBody.Multipart(
            [new MultipartPart("a", RequestBody.FromString("x")), new MultipartPart("b", RequestBody.FromStream(new NonSeekable(new byte[] { 1 })))]), false);
    }

    private sealed class NonSeekable(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    [Theory]
    [MemberData(nameof(Methods))]
    public void IsResendable_matrix(string methodName)
    {
        var method = Method.Of(methodName);
        foreach (var (name, make, replayable) in Bodies())
        {
            var body = make();
            Request request;
            try
            {
                request = new Request(method, new Uri("https://example.test/"), null, body);
            }
            catch (ArgumentException)
            {
                // The model itself refuses a body on GET and HEAD; there is no such request to re-send.
                continue;
            }

            // The expected value is computed from the definition, not from IsResendable.
            var expected = body is null ? new[] { "GET", "HEAD", "OPTIONS", "PUT", "DELETE" }.Contains(methodName) : replayable;

            Assert.True(expected == RetryFacts.IsResendable(request), $"{methodName} / {name}");
        }
    }

    [Fact]
    public void A_bare_POST_is_not_resendable_but_a_POST_with_a_replayable_body_is()
    {
        var bare = new Request(Method.Post, new Uri("https://example.test/"));
        var withBody = Request.Post("https://example.test/", RequestBody.FromString("{}"));
        var streamed = Request.Post("https://example.test/", RequestBody.FromStream(new NonSeekable([1])));

        Assert.False(RetryFacts.IsResendable(bare));
        Assert.True(RetryFacts.IsResendable(withBody));
        Assert.False(RetryFacts.IsResendable(streamed));
    }

    [Fact]
    public void Method_IsIdempotent_reads_the_moved_set()
    {
        Assert.True(Method.Get.IsIdempotent);
        Assert.False(Method.Post.IsIdempotent);
        Assert.Same(RetryFacts.IdempotentMethods, RetryFacts.IdempotentMethods);
    }
}
