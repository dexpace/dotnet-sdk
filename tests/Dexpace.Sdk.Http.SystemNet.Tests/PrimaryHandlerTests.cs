// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-1, TRANSPORT-15, TRANSPORT-30, CFG-28 (phase 9, P9-12, R6): <see cref="SystemNetHttpClient.CreatePrimaryHandler"/>,
/// the one place the owned-handler invariants are built, shared by the owned construction and a factory-built chain.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PrimaryHandlerTests
{
    private static readonly Uri s_destination = new("http://example.test/");

    [Fact]
    public void CreatePrimaryHandler_goes_direct_and_never_follows_redirects()
    {
        using var handler = SystemNetHttpClient.CreatePrimaryHandler(new SystemNetHttpClientOptions());

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.Null(handler.Proxy);
        Assert.Null(handler.Credentials);
    }

    [Fact]
    public void CreatePrimaryHandler_installs_the_proxy_adapter_and_leaves_server_credentials_unset()
    {
        var options = new SystemNetHttpClientOptions
        {
            Proxy = new ProxyOptions { Host = "proxy.test", Port = 3128, UserName = "u", Password = "p" },
        };

        using var handler = SystemNetHttpClient.CreatePrimaryHandler(options);

        Assert.True(handler.UseProxy);
        Assert.NotNull(handler.Proxy);
        Assert.Equal("http://proxy.test:3128/", handler.Proxy!.GetProxy(s_destination)!.AbsoluteUri);
        Assert.NotNull(handler.Proxy.Credentials!.GetCredential(handler.Proxy.GetProxy(s_destination)!, "Basic"));
        Assert.Null(handler.Proxy.Credentials.GetCredential(handler.Proxy.GetProxy(s_destination)!, "NTLM"));
        Assert.Null(handler.Credentials);
    }

    [Fact]
    public void CreatePrimaryHandler_warns_once_through_the_options_logger()
    {
        var logger = new RecordingLogger();
        var options = new SystemNetHttpClientOptions
        {
            Logger = logger,
            Proxy = new ProxyOptions { Type = ProxyType.Socks4, Host = "proxy.test", Port = 1080, UserName = "u", Password = "p" },
        };

        using (SystemNetHttpClient.CreatePrimaryHandler(options))
        {
        }

        Assert.Equal(DexpaceLogEvents.TransportProxyFeatureUnsupportedId, Assert.Single(logger.Entries).EventId.Id);
    }

    [Fact]
    public void The_owned_construction_still_warns_exactly_once()
    {
        // R6's pin: the warning moved into CreatePrimaryHandler, it did not double.
        var logger = new RecordingLogger();
        var options = new SystemNetHttpClientOptions
        {
            Logger = logger,
            Proxy = new ProxyOptions { Type = ProxyType.Socks4, Host = "proxy.test", Port = 1080, UserName = "u", Password = "p" },
        };

        using (new SystemNetHttpClient(options))
        {
        }

        Assert.Equal(DexpaceLogEvents.TransportProxyFeatureUnsupportedId, Assert.Single(logger.Entries).EventId.Id);
    }

    [Fact]
    public void The_returned_handler_is_accepted_by_the_handler_constructor_and_never_disposed_by_it()
    {
        using var handler = SystemNetHttpClient.CreatePrimaryHandler(new SystemNetHttpClientOptions());

        using (new SystemNetHttpClient(handler, new SystemNetHttpClientOptions()))
        {
        }

        // TRANSPORT-15: the handler is the caller's; a disposed SocketsHttpHandler would throw on a property write.
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
    }

    [Fact]
    public void CreatePrimaryHandler_rejects_null()
    {
        Assert.Throws<ArgumentNullException>("options", () => SystemNetHttpClient.CreatePrimaryHandler(null!));
    }
}
