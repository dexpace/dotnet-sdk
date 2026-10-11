// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Assertions;

/// <summary>
/// The wire scenario of <c>transport-2.bodyless-not-resent</c>. Its verdict on a native client turns on whether the client's read
/// for the response reports an end of stream or a reset, so the reply that ends each unanswered exchange is pinned here: a revert
/// to <see cref="LoopbackResponse.Abort"/> still passes on Linux, which reads its FIN first, and fails only some runs on macOS.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConcurrencyAssertionsTests
{
    [Fact]
    public void The_bodyless_scenario_closes_an_unanswered_exchange_in_order_and_never_resets_it()
    {
        var reply = ConcurrencyAssertions.ClosedUnanswered();

        Assert.Equal(ReplyMode.Normal, reply.Mode);
        Assert.True(reply.Bytes.IsEmpty, "no response byte may precede the close");
        Assert.True(reply.CloseConnection, "the server must close the connection after reading the request");
        Assert.Null(reply.Chunks);
        Assert.Null(reply.Gate);
    }
}
