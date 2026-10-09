// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// SERDE-28 / XCUT-19 (P7a-19, P7a-23): a response the status-aware handler can neither decode nor map (a 3xx nobody followed)
/// fails with a message that names the <c>Location</c>, and an exception message is logged and shown. A redirect target
/// routinely carries a credential in its query or userinfo, so the message carries the target only through
/// <c>UrlRedactor</c>'s default-deny redaction. Permanent: never delete or loosen a test in this class.
/// </summary>
[Trait("Category", "Security")]
public sealed class StatusAwareHandlerLocationRedactionTests
{
    private static async Task<string> MessageForAsync(string location, Request? request = null)
    {
        var headers = new Headers.Builder().AddInbound("Location", location).Build();
        var response = TestResponses.Create(Status.Found, request, headers, new CountingPayloadBody([]));

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                .HandleAsync(response, TestContext.Current.CancellationToken));
        return ex.Message;
    }

    [Fact]
    public async Task A_302_with_a_secret_in_the_Location_query_does_not_leak_it()
    {
        var message = await MessageForAsync("https://h/p?access_token=s3cr3t");

        Assert.DoesNotContain("s3cr3t", message, StringComparison.Ordinal);
        Assert.Contains(new UrlRedactor().Redact(new Uri("https://h/p?access_token=s3cr3t")), message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_relative_Location_secret_does_not_leak_either()
    {
        var message = await MessageForAsync("/p?access_token=s3cr3t", Request.Get("https://h/start"));

        Assert.DoesNotContain("s3cr3t", message, StringComparison.Ordinal);
        Assert.Contains("https://h/p", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_message_does_not_contain_userinfo()
    {
        var message = await MessageForAsync("https://u:p4ss@h/p");

        Assert.DoesNotContain("p4ss", message, StringComparison.Ordinal);
        Assert.DoesNotContain("u:p4ss", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Location_with_obs_text_or_HTAB_is_rendered_without_splitting_the_message()
    {
        // Headers.Builder.AddInbound rejects CR, LF and every other control character except HTAB, so a CR/LF fixture cannot be
        // built; HTAB and obs-text are the most a received header can carry. The message stays one line.
        var headers = new Headers.Builder()
            .AddInbound("Location", "https://h/p\tq?x=é")
            .AddInbound("ETag", "\"a\tbé\"")
            .Build();
        var response = TestResponses.Create(Status.Found, null, headers, new CountingPayloadBody([]));

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                .HandleAsync(response, TestContext.Current.CancellationToken));

        Assert.DoesNotContain('\r', ex.Message);
        Assert.DoesNotContain('\n', ex.Message);
        Assert.StartsWith("302", ex.Message, StringComparison.Ordinal);
    }
}
