// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>Proxy credentials answer the proxy's challenge and nothing else, and are never logged (<c>TRANSPORT-30</c>). The row belongs to phase 8b; the assertion is 8a's.</summary>
internal static class ProxyAssertions
{
    private const string User = "proxy-user";
    private const string Secret = "proxy-secret";

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-30.proxy-discoverable-no-leak", ["TRANSPORT-30"], RequirementLevel.Should, AsyncOnly, ProxyDiscoverableNoLeakAsync);
    }

    // TRANSPORT-30: the fixture is a forward proxy that answers 407, then relays an origin 401. The proxy credential answers
    // the 407, is never offered to the origin's 401, never carried as an Authorization header, and never logged.
    private static async Task ProxyDiscoverableNoLeakAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var proxy = context.StartServer(request => request.HeaderValues("Proxy-Authorization").Count > 0
            ? LoopbackResponse.Status(401, "Unauthorized", [new("WWW-Authenticate", "Basic realm=\"origin\"")], keepAlive: true)
            : LoopbackResponse.Status(407, "Proxy Authentication Required", [new("Proxy-Authenticate", "Basic realm=\"proxy\"")], keepAlive: true));
        var options = new ProxyOptions { Host = "127.0.0.1", Port = proxy.BaseUri.Port, UserName = User, Password = Secret };
        var transport = context.RequireProxy(options);

        using var response = await transport.ExpectResponseAsync(Request.Get("http://origin.test/resource"), "a GET through a proxy that challenges with 407", cancellationToken).ConfigureAwait(false);

        var requests = proxy.Requests;
        Check.True(requests.Any(request => request.HeaderValues("Proxy-Authorization").Count > 0), "the transport should answer the proxy's 407 with the proxy credential (TRANSPORT-30: fall back to Basic from the user name and password)", "a request carrying Proxy-Authorization", "none");
        Check.True(requests.All(request => request.HeaderValues("Authorization").Count == 0), "the proxy credential must never be answered to an origin 401: no request may carry an Authorization header (TRANSPORT-30)", "no Authorization header", "an Authorization header was sent");
        Check.Equal(response.Status.Code, 401, "the origin's 401, delivered as it is");
        Check.True(!context.Logger.Leaks(User) && !context.Logger.Leaks(Secret), "the proxy credential must never be logged (TRANSPORT-30)", "no log entry carrying the user name or password", "a credential was logged");
    }
}
