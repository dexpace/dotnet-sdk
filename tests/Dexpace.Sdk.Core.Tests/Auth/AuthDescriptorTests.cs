// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Auth;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-1 to AUTH-3: the scheme enum, the requirement and the descriptor.</summary>
[Trait("Category", "Unit")]
public sealed class AuthDescriptorTests
{
    [Fact]
    public void AuthScheme_values_are_explicit_and_exactly_five()
    {
        Assert.Equal(0, (int)AuthScheme.OAuth2);
        Assert.Equal(1, (int)AuthScheme.ApiKey);
        Assert.Equal(2, (int)AuthScheme.Basic);
        Assert.Equal(3, (int)AuthScheme.Digest);
        Assert.Equal(4, (int)AuthScheme.NoAuth);
        Assert.Equal(5, Enum.GetValues<AuthScheme>().Length);
        Assert.Equal(AuthScheme.OAuth2, default(AuthScheme));
        Assert.NotEqual(AuthScheme.NoAuth, default(AuthScheme));
    }

    [Fact]
    public void A_requirement_rejects_an_undefined_scheme()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthRequirement((AuthScheme)99));
    }

    [Fact]
    public void NoAuth_is_the_anonymous_sentinel()
    {
        Assert.Equal(AuthScheme.NoAuth, AuthRequirement.NoAuth.Scheme);
        Assert.Equal(AuthRequirement.NoAuth, AuthRequirement.NoAuth);
    }

    [Fact]
    public void Scopes_and_parameters_are_copied_at_init()
    {
        var scopes = new List<string> { "a" };
        var parameters = new Dictionary<string, string> { ["k"] = "v" };
        var requirement = new AuthRequirement(AuthScheme.OAuth2) { Scopes = scopes, Parameters = parameters };

        scopes.Add("b");
        parameters["k2"] = "v2";

        Assert.Equal(["a"], requirement.Scopes);
        Assert.Single(requirement.Parameters);
        Assert.Empty(new AuthRequirement(AuthScheme.Basic).Scopes);
        Assert.Empty(new AuthRequirement(AuthScheme.Basic).Parameters);
        Assert.Throws<ArgumentException>(() => new AuthRequirement(AuthScheme.OAuth2) { Scopes = new string?[] { null }! });
    }

    [Fact]
    public void Scopes_and_parameters_are_preserved_and_never_interpreted()
    {
        var requirement = new AuthRequirement(AuthScheme.OAuth2)
        {
            Scopes = ["a", "b"],
            Parameters = new Dictionary<string, string> { ["k"] = "v" },
        };

        Assert.Equal(["a", "b"], requirement.Scopes);
        Assert.Equal("v", requirement.Parameters["k"]);
    }

    [Fact]
    public void Requirements_with_equal_content_are_equal_and_hash_alike()
    {
        var a = new AuthRequirement(AuthScheme.OAuth2) { Scopes = new List<string> { "x", "y" }, Parameters = new Dictionary<string, string> { ["k"] = "v" } };
        var b = new AuthRequirement(AuthScheme.OAuth2) { Scopes = new List<string> { "x", "y" }, Parameters = new Dictionary<string, string> { ["k"] = "v" } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Scopes = ["y", "x"] });
        Assert.NotEqual(a, a with { Parameters = new Dictionary<string, string> { ["k"] = "other" } });
    }

    [Fact]
    public void A_requirement_does_not_print_a_parameter_value()
    {
        var requirement = new AuthRequirement(AuthScheme.ApiKey) { Parameters = new Dictionary<string, string> { ["tenant"] = "t-Zx9" } };

        Assert.DoesNotContain("t-Zx9", requirement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_descriptor_copies_its_requirements_and_exposes_them_read_only()
    {
        var source = new[] { new AuthRequirement(AuthScheme.Basic), new AuthRequirement(AuthScheme.Digest) };
        var descriptor = new AuthDescriptor(source);

        source[0] = AuthRequirement.NoAuth;

        Assert.Equal(AuthScheme.Basic, descriptor.Requirements[0].Scheme);
        // ImmutableArray<T> implements IList<T> explicitly; every mutator throws.
        Assert.Throws<NotSupportedException>(() => ((IList<AuthRequirement>)descriptor.Requirements).Add(AuthRequirement.NoAuth));
    }

    [Fact]
    public void An_empty_descriptor_or_a_null_element_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AuthDescriptor(Array.Empty<AuthRequirement>()));
        Assert.Throws<ArgumentException>(() => new AuthDescriptor(new AuthRequirement?[] { null }!));
    }

    [Fact]
    public void AllowsAnonymous_is_true_iff_any_requirement_is_NoAuth()
    {
        Assert.True(new AuthDescriptor(AuthRequirement.NoAuth).AllowsAnonymous);
        Assert.True(new AuthDescriptor(new AuthRequirement(AuthScheme.Basic), AuthRequirement.NoAuth).AllowsAnonymous);
        Assert.False(new AuthDescriptor(new AuthRequirement(AuthScheme.Basic)).AllowsAnonymous);
    }

    [Fact]
    public void Descriptors_with_the_same_ordered_requirements_are_equal()
    {
        var a = new AuthRequirement(AuthScheme.Basic);
        var b = new AuthRequirement(AuthScheme.Digest);

        Assert.Equal(new AuthDescriptor(a, b), new AuthDescriptor(a, b));
        Assert.Equal(new AuthDescriptor(a, b).GetHashCode(), new AuthDescriptor(a, b).GetHashCode());
        Assert.NotEqual(new AuthDescriptor(a, b), new AuthDescriptor(b, a));
    }

    [Fact]
    public void A_descriptor_has_no_init_member_so_with_cannot_empty_it()
    {
        foreach (var property in typeof(AuthDescriptor).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var setter = property.SetMethod;
            if (setter is null)
            {
                continue;
            }

            Assert.DoesNotContain(
                setter.ReturnParameter.GetRequiredCustomModifiers(),
                modifier => modifier == typeof(IsExternalInit));
        }
    }

    [Fact]
    public void Scheme_names_print_without_culture_dependence()
    {
        using var _ = new CultureScope("tr-TR");
        Assert.Contains("Digest", new AuthDescriptor(new AuthRequirement(AuthScheme.Digest)).ToString(), StringComparison.Ordinal);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
