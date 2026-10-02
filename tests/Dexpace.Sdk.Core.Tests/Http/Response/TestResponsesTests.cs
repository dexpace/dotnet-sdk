// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>The test-support factory every test now builds <see cref="Response"/> through (phase 2a, task 7.1).</summary>
[Trait("Category", "Unit")]
public class TestResponsesTests
{
    [Fact]
    public async Task Create_passes_status_headers_body_and_protocol_through()
    {
        var headers = Headers.Empty.With("X-A", "1");
        var body = ResponseBody.FromBytes(Encoding.UTF8.GetBytes("hello"));

        using var response = TestResponses.Create(Status.Created, headers: headers, body: body, protocol: Protocol.Http2);

        Assert.Equal(Status.Created, response.Status);
        Assert.Equal(headers, response.Headers);
        Assert.Same(body, response.Body);
        Assert.Equal(Protocol.Http2, response.Protocol);
        Assert.Equal("hello", await body.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Create_defaults_headers_body_and_protocol()
    {
        using var response = TestResponses.Create(Status.Ok);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Empty(response.Headers);
        Assert.NotNull(response.Body);
        Assert.Equal(Protocol.Http11, response.Protocol);
    }
}
