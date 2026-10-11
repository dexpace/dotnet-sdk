// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using EventLogger = Dexpace.Sdk.TestSupport.Diagnostics.RecordingLogger;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Security;

/// <summary>
/// Phase 8b, TRANSPORT-30's MUST (proxy credentials answer the proxy and nothing else, and are never logged), with XCUT-18
/// and CFG-22 (design §8.2, P8b-5). The loopback fixture is a forward proxy: an <c>http://</c> request reaches it in absolute
/// form, it answers <c>407</c> until it sees <c>Proxy-Authorization</c>, then relays an origin's <c>401</c>. The risk this pins is
/// the runtime answering an origin's <c>WWW-Authenticate</c> with the proxy's pair, which it does when a handler's server
/// <c>Credentials</c> are set. Permanent (roadmap constraint 5); the adapter's unit rows are <c>ProxyAdapterTests</c>.
/// </summary>
[Trait("Category", "Security")]
public sealed class ProxyCredentialWireTests
{
    private const string User = "u-7f3a";
    private const string Secret = "p-91c2";
    private const string Origin = "http://origin.invalid/resource";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Basic(string user, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password));

    // A forward proxy that challenges with Basic until the request carries Proxy-Authorization, then lets an origin's 401 through.
    private static LoopbackServer StartProxy() => LoopbackServer.Start(request =>
        request.HeaderValues("Proxy-Authorization").Count > 0
            ? LoopbackResponse.Status(401, "Unauthorized", [new("WWW-Authenticate", "Basic realm=\"origin\"")], keepAlive: true)
            : LoopbackResponse.Status(407, "Proxy Authentication Required", [new("Proxy-Authenticate", "Basic realm=\"proxy\"")], keepAlive: true));

    private static ProxyOptions ProxyAt(LoopbackServer server, ICredentials? hook = null) => new()
    {
        Host = "127.0.0.1",
        Port = server.BaseUri.Port,
        UserName = User,
        Password = Secret,
        ChallengeCredentials = hook,
    };

    private static SystemNetHttpClient Transport(ProxyOptions proxy, EventLogger? logger = null) =>
        new(new SystemNetHttpClientOptions { Proxy = proxy, Logger = logger ?? new EventLogger() });

    [Fact]
    public async Task The_proxys_407_is_answered_with_the_configured_basic_pair()
    {
        await using var proxy = StartProxy();
        await using var transport = Transport(ProxyAt(proxy));

        using var response = await transport.ExecuteAsync(Request.Get(Origin), Ct);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(2, proxy.Requests.Count);
        Assert.Empty(proxy.Requests[0].HeaderValues("Proxy-Authorization"));
        Assert.Equal(Basic(User, Secret), proxy.Requests[1].Header("Proxy-Authorization"));
    }

    [Fact]
    public async Task An_origin_401_behind_the_proxy_is_never_answered_with_the_proxy_credential()
    {
        await using var proxy = StartProxy();
        await using var transport = Transport(ProxyAt(proxy));

        using var response = await transport.ExecuteAsync(Request.Get(Origin), Ct);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal("Basic realm=\"origin\"", response.Headers.Get("WWW-Authenticate"));
        Assert.Equal(2, proxy.Requests.Count);
        foreach (var request in proxy.Requests)
        {
            Assert.Empty(request.HeaderValues("Authorization"));

            // The pair, in any encoding, appears in no header but Proxy-Authorization.
            var otherLines = request.HeaderLines.Where(line => !line.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase));
            foreach (var line in otherLines)
            {
                Assert.DoesNotContain(User, line, StringComparison.Ordinal);
                Assert.DoesNotContain(Secret, line, StringComparison.Ordinal);
                Assert.DoesNotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes(User + ":" + Secret)), line, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task A_digest_challenge_is_not_answered_by_the_basic_fallback()
    {
        await using var proxy = LoopbackServer.Start(_ => LoopbackResponse.Status(
            407,
            "Proxy Authentication Required",
            [new("Proxy-Authenticate", "Digest realm=\"p\", nonce=\"abc\", qop=\"auth\"")],
            keepAlive: true));
        await using var transport = Transport(ProxyAt(proxy));

        using var response = await transport.ExecuteAsync(Request.Get(Origin), Ct);

        Assert.Equal(407, response.Status.Code);
        Assert.Single(proxy.Requests);
        Assert.Empty(proxy.Requests[0].HeaderValues("Proxy-Authorization"));
        Assert.DoesNotContain(User, proxy.Requests[0].RawText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_log_entry_at_any_level_and_no_failure_message_contains_the_user_name_or_password()
    {
        var logger = new EventLogger();
        await using var proxy = StartProxy();
        await using var transport = Transport(ProxyAt(proxy), logger);

        // A caller-set framing header, so the exchange also exercises the drop log.
        using var response = await transport.ExecuteAsync(Request.Get(Origin).WithHeader("Connection", "keep-alive"), Ct);

        // And an unreachable proxy, so the failure path's messages are read too.
        var unreachable = Transport(new ProxyOptions { Host = "127.0.0.1", Port = FreePort(), UserName = User, Password = Secret }, logger);
        await using (unreachable)
        {
            var failure = await Assert.ThrowsAsync<ServiceRequestException>(() => unreachable.ExecuteAsync(Request.Get(Origin), Ct));
            AssertNoSecret(failure.ToString());
        }

        Assert.NotEmpty(logger.Entries);
        foreach (var entry in logger.Entries)
        {
            AssertNoSecret(entry.Message);
            AssertNoSecret(string.Join('|', entry.State.Select(pair => $"{pair.Key}={pair.Value}")));
            AssertNoSecret(entry.Exception?.ToString() ?? string.Empty);
        }

        AssertNoSecret(new SystemNetHttpClientOptions { Proxy = ProxyAt(proxy) }.ToString());
    }

    [Fact]
    public async Task A_challenge_credentials_hook_wins_over_the_pair_and_the_pair_is_never_sent()
    {
        var hook = new FixedCredentials("hook-user", "hook-pass");
        var logger = new EventLogger();
        await using var proxy = StartProxy();
        await using var transport = Transport(ProxyAt(proxy, hook), logger);

        using var response = await transport.ExecuteAsync(Request.Get(Origin), Ct);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(Basic("hook-user", "hook-pass"), proxy.Requests[^1].Header("Proxy-Authorization"));
        Assert.DoesNotContain(proxy.Requests, request => request.RawText.Contains(Secret, StringComparison.Ordinal));
        Assert.Contains(hook.AuthTypes, authType => string.Equals(authType, "Basic", StringComparison.OrdinalIgnoreCase));

        // The pair is ignored in favour of the hook: the construction said so once, without a value.
        var warning = Assert.Single(logger.Entries, entry => entry.EventId.Id == Dexpace.Sdk.Core.Diagnostics.DexpaceLogEvents.TransportProxyFeatureUnsupportedId);
        Assert.DoesNotContain(User, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_proxy_uri_given_to_the_runtime_has_no_userinfo()
    {
        SocketsHttpHandler? seen = null;
        var options = new SystemNetHttpClientOptions { Proxy = new ProxyOptions { Host = "proxy.test", Port = 3128, UserName = User, Password = Secret } };
        using var transport = new SystemNetHttpClient(handler => seen = handler, options);

        var uri = seen!.Proxy!.GetProxy(new Uri(Origin))!;

        Assert.Equal(string.Empty, uri.UserInfo);
        AssertNoSecret(uri.OriginalString);
        Assert.Null(seen.Credentials);
        Assert.Null(seen.DefaultProxyCredentials);
    }

    private static void AssertNoSecret(string text)
    {
        Assert.DoesNotContain(User, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
    }

    // A port nothing listens on: bind to 0, read the number, release it.
    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class FixedCredentials(string user, string password) : ICredentials
    {
        private readonly List<string> _authTypes = [];

        public IReadOnlyList<string> AuthTypes
        {
            get
            {
                lock (_authTypes)
                {
                    return [.. _authTypes];
                }
            }
        }

        public NetworkCredential? GetCredential(Uri uri, string authType)
        {
            lock (_authTypes)
            {
                _authTypes.Add(authType);
            }

            return string.Equals(authType, "Basic", StringComparison.OrdinalIgnoreCase) ? new NetworkCredential(user, password) : null;
        }
    }
}
