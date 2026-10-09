// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// AUTH-16, AUTH-22, AUTH-30 at the wire (design fact 9, P6c-34): the Digest <c>uri</c> parameter equals the request-target
/// the transport writes, for an escaped path, an escaped query and an empty path.
/// </summary>
/// <remarks>
/// The loopback server speaks plain <c>http</c> and the auth guard has no loopback exemption (design §11 item 25), so the test
/// does not go through an auth policy: it answers a parsed challenge with the handler and sends the stamped request straight
/// through the transport.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class DigestWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    [Theory]
    [InlineData("a%2Fb?x=%26y")]
    [InlineData("p%20q/r?a=b%20c")]
    [InlineData("")]
    [InlineData("?q=1")]
    public async Task The_uri_parameter_equals_the_request_target_the_server_received(string pathAndQuery)
    {
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Status(401, "Unauthorized", [new("WWW-Authenticate", "Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm=SHA-256")]),
            LoopbackResponse.Ok("ok"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var handler = new DigestChallengeHandler(new DigestCredential("u", "p"));
        var url = server.BaseUri.GetLeftPart(UriPartial.Authority) + (pathAndQuery.StartsWith('?') ? string.Empty : "/") + pathAndQuery;
        var request = Request.Get(url);

        await using var challenge = await transport.ExecuteAsync(request, Ct);
        var challenges = challenge.Headers.GetAll("WWW-Authenticate").SelectMany(AuthenticationChallenge.Parse).ToList();
        var answered = handler.Authorize(challenges, request, proxy: false);
        Assert.NotNull(answered);
        await using var second = await transport.ExecuteAsync(answered, Ct);

        Assert.Equal(2, server.Requests.Count);
        var recorded = server.Requests[1];
        var authorization = Assert.Single(recorded.HeaderLines, line => line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase));
        var uri = AuthenticationChallenge.Parse(authorization["Authorization:".Length..].Trim()).Single().Parameters["uri"];
        Assert.Equal(recorded.Target, uri);
        Assert.Equal(200, second.Status.Code);
    }
}
