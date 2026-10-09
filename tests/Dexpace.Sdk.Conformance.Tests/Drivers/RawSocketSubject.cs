// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.RawSocket;
using Dexpace.Sdk.Core.Client;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// The second conformance subject (D3, P8a-2): the test-only raw-socket client reached through
/// <c>DelegateHttpClient</c>, so the assertions are proven to be about the contract and not about <c>SocketsHttpHandler</c>.
/// It is honest about what it does not do: no native client to borrow, no proxy, no internal cancel, no native re-send, no
/// header-drop logging; the SHOULDs it does not implement are waived permanently with the reason "test-only transport".
/// </summary>
internal static class RawSocketSubject
{
    /// <summary>The raw-socket client as a subject on both faces.</summary>
    internal static TransportSubject Create() => new()
    {
        Name = "RawSocketHttpClient through DelegateHttpClient (test-only)",
        CreateAsync = _ =>
        {
            var raw = new RawSocketHttpClient();
            return DelegateHttpClient.Create(raw.ExecuteAsync);
        },
        CreateBlocking = _ =>
        {
            var blocking = new RawSocketHttpClient().AsBlocking();
            return DelegateHttpClient.CreateBlocking(blocking.Execute);
        },
    };

    /// <summary>The options of the driver: the bounds, and the waivers listed in the design (plan reading R4).</summary>
    internal static TransportSuiteOptions Options { get; } = new()
    {
        AssertionTimeout = TimeSpan.FromSeconds(10),
        Waivers =
        [
            new("TRANSPORT-13", "test-only transport: it drops no header, so it has no drop-logging policy to implement"),
            new("TRANSPORT-11", "test-only transport: it computes the framing headers but logs nothing about the caller's copies it drops")
            {
                Assertion = "transport-11.drop-logged",
            },
        ],
    };
}
