// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-18, AUTH-19, AUTH-24: the per-nonce counter, through the handler.</summary>
[Trait("Category", "Unit")]
public sealed class NonceCounterTests
{
    private static DigestChallengeHandler NewHandler() =>
        new(new DigestCredential("u", "p"), DigestChallengeHandler.DefaultPreference, md5Available: true, () => "cn");

    private static string Nc(DigestChallengeHandler handler, string nonce)
    {
        var challenges = AuthenticationChallenge.Parse($"Digest realm=\"r\", nonce=\"{nonce}\", qop=\"auth\"");
        var header = handler.Authorize(challenges, Request.Get("https://h/x"), proxy: false)!.Headers.Get("Authorization")!;
        return AuthenticationChallenge.Parse(header).Single().Parameters["nc"];
    }

    [Fact]
    public void First_use_of_a_nonce_is_nc_00000001()
    {
        Assert.Equal("00000001", Nc(NewHandler(), "n"));
    }

    [Fact]
    public void Reuse_of_the_same_nonce_increments()
    {
        var handler = NewHandler();

        Assert.Equal("00000001", Nc(handler, "n"));
        Assert.Equal("00000002", Nc(handler, "n"));
        Assert.Equal("00000003", Nc(handler, "n"));
    }

    [Fact]
    public void A_different_nonce_starts_at_one()
    {
        var handler = NewHandler();
        _ = Nc(handler, "a");

        Assert.Equal("00000001", Nc(handler, "b"));
    }

    [Fact]
    public void Nc_is_eight_lower_case_hex_digits_of_the_low_32_bits()
    {
        var handler = NewHandler();
        handler.SetNextCount("n", 0xABCDEF);
        Assert.Equal("00abcdef", Nc(handler, "n"));

        handler.SetNextCount("w", uint.MaxValue);
        Assert.Equal("ffffffff", Nc(handler, "w"));
        Assert.Equal("00000000", Nc(handler, "w"));
    }

    [Fact]
    public void The_store_is_a_BoundedMap_of_1024()
    {
        Assert.Equal(1024, NewHandler().NonceStoreCapacity);
        Assert.Equal(1024, new BoundedMap<string, NonceCounter>(1024).Capacity);
    }

    [Fact]
    public async Task Sixty_four_parallel_Authorize_calls_on_one_nonce_yield_nc_1_to_64_without_duplicates()
    {
        var handler = NewHandler();

        var counts = await Task.WhenAll(Enumerable.Range(0, 64).Select(
            _ => Task.Run(() => Convert.ToInt32(Nc(handler, "shared"), 16), TestContext.Current.CancellationToken)));

        Assert.Equal(Enumerable.Range(1, 64), counts.Order());
    }

    [Fact]
    public void Handlers_hold_no_state_except_the_counters()
    {
        var fields = typeof(DigestChallengeHandler).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(f => f.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["_cnonceFactory", "_credential", "_md5Available", "_nonces", "_preference"], fields);
    }

    [Fact]
    public async Task The_composite_and_basic_handlers_are_stateless_too()
    {
        foreach (var type in new[] { typeof(CompositeChallengeHandler), typeof(BasicChallengeHandler) })
        {
            var mutable = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(f => !f.IsInitOnly);
            Assert.Empty(mutable);
        }

        await Task.CompletedTask;
    }
}
