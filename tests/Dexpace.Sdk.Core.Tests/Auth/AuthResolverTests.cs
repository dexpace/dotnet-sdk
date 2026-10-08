// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-4 to AUTH-7: the tiers, the preference order and the statelessness of the resolver.</summary>
[Trait("Category", "Unit")]
public sealed class AuthResolverTests
{
    private static AuthDescriptor Of(params AuthScheme[] schemes) =>
        new(schemes.Select(static s => new AuthRequirement(s)));

    [Fact]
    public void Per_call_wins_over_operation_and_client()
    {
        var chosen = AuthResolver.Resolve(Of(AuthScheme.Basic), Of(AuthScheme.Digest), Of(AuthScheme.OAuth2), [AuthScheme.Basic, AuthScheme.Digest, AuthScheme.OAuth2]);

        Assert.Equal(AuthScheme.Basic, chosen.Scheme);
    }

    [Fact]
    public void Operation_wins_over_client()
    {
        var chosen = AuthResolver.Resolve(null, Of(AuthScheme.Digest), Of(AuthScheme.OAuth2), [AuthScheme.Digest, AuthScheme.OAuth2]);

        Assert.Equal(AuthScheme.Digest, chosen.Scheme);
    }

    [Fact]
    public void Client_is_used_when_the_others_are_absent()
    {
        var chosen = AuthResolver.Resolve(null, null, Of(AuthScheme.OAuth2), [AuthScheme.OAuth2]);

        Assert.Equal(AuthScheme.OAuth2, chosen.Scheme);
    }

    [Fact]
    public void A_higher_tier_that_cannot_be_satisfied_does_not_fall_through()
    {
        Assert.Throws<AuthResolutionException>(
            () => AuthResolver.Resolve(Of(AuthScheme.Digest), null, Of(AuthScheme.OAuth2), [AuthScheme.OAuth2]));
    }

    [Fact]
    public void The_first_satisfiable_requirement_in_preference_order_wins()
    {
        var chosen = AuthResolver.Resolve(null, null, Of(AuthScheme.Digest, AuthScheme.OAuth2, AuthScheme.Basic), [AuthScheme.OAuth2, AuthScheme.Basic]);

        Assert.Equal(AuthScheme.OAuth2, chosen.Scheme);
    }

    [Fact]
    public void NoAuth_is_always_satisfiable()
    {
        Assert.Equal(AuthScheme.NoAuth, AuthResolver.Resolve(null, null, Of(AuthScheme.NoAuth), []).Scheme);
        Assert.Equal(AuthScheme.NoAuth, AuthResolver.Resolve(null, null, Of(AuthScheme.Digest, AuthScheme.NoAuth), []).Scheme);
    }

    [Fact]
    public void No_credential_is_inspected()
    {
        var method = typeof(AuthResolver).GetMethod(nameof(AuthResolver.Resolve))!;

        Assert.All(
            method.GetParameters(),
            p => Assert.DoesNotContain("Credential", p.ParameterType.Name, StringComparison.Ordinal));
        Assert.Equal(typeof(IReadOnlyCollection<AuthScheme>), method.GetParameters()[3].ParameterType);
    }

    [Fact]
    public void All_tiers_absent_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AuthResolver.Resolve(null, null, null, [AuthScheme.Basic]));
    }

    [Fact]
    public void Nothing_satisfiable_throws_AuthResolutionException_with_required_and_available()
    {
        var descriptor = new AuthDescriptor(
            new AuthRequirement(AuthScheme.Digest),
            new AuthRequirement(AuthScheme.OAuth2) { Scopes = ["secret-scope"] });

        var ex = Assert.Throws<AuthResolutionException>(
            () => AuthResolver.Resolve(null, null, descriptor, [AuthScheme.Basic, AuthScheme.ApiKey]));

        Assert.Equal([AuthScheme.Digest, AuthScheme.OAuth2], ex.Required);
        Assert.Equal([AuthScheme.ApiKey, AuthScheme.Basic], ex.Available);
        Assert.Contains("[Digest, OAuth2]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("[ApiKey, Basic]", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-scope", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_is_safe_under_concurrent_calls_on_one_descriptor()
    {
        var descriptor = Of(AuthScheme.Digest, AuthScheme.Basic);
        var tasks = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => AuthResolver.Resolve(null, null, descriptor, [AuthScheme.Basic]), TestContext.Current.CancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(AuthScheme.Basic, r.Scheme));
        var type = typeof(AuthResolver);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }
}
