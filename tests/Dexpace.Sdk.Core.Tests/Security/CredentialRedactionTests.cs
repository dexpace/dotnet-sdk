// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S5 remainder (XCUT-19(d); design §6.3 fact 2, P6c-20): no credential type ever reveals its secret in
/// its string form, however it is formatted. Every credential overrides <c>ToString</c> by hand, so the redaction holds
/// whatever the type becomes. Permanent (roadmap constraint 5): never delete or loosen a case.
/// </summary>
[Trait("Category", "Security")]
public sealed class CredentialRedactionTests
{
    private const string Secret = "s3cr3t-Zx9";

    public static TheoryData<object, string> Credentials => new()
    {
        { new AccessToken(Secret, DateTimeOffset.UnixEpoch), Secret },
        { new ApiKeyCredential(Secret, HttpHeaderName.Of("X-Api-Key")), Secret },
        { new BasicCredential("alice", Secret), Secret },
        { new DigestCredential("alice", Secret), Secret },
    };

    [Theory]
    [MemberData(nameof(Credentials))]
    public void Every_credential_type_formats_without_its_secret(object value, string secret)
    {
        var formats = new[]
        {
            value.ToString()!,
            $"{value}",
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value),
            ((object)value).ToString()!,
        };

        foreach (var text in formats)
        {
            Assert.Contains("***", text, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AuthCredentials_with_every_member_set_lists_schemes_and_leaks_nothing()
    {
        var credentials = new AuthCredentials
        {
            Token = new FixedTokenCredential(),
            ApiKey = new ApiKeyCredential(Secret),
            Basic = new BasicCredential("alice", Secret),
            Digest = new DigestCredential("alice", Secret),
        };

        var text = credentials.ToString();

        Assert.Contains("OAuth2", text, StringComparison.Ordinal);
        Assert.Contains("ApiKey", text, StringComparison.Ordinal);
        Assert.Contains("Basic", text, StringComparison.Ordinal);
        Assert.Contains("Digest", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", text, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.Basic.ToString(), text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_that_wraps_a_credential_does_not_reintroduce_the_secret_unsuspected()
    {
        var holder = new Holder(new BasicCredential("alice", Secret));

        var text = holder.ToString();

        Assert.Contains("***", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_public_type_in_the_Auth_namespace_that_holds_a_secret_overrides_ToString()
    {
        var types = typeof(AccessToken).Assembly.GetTypes()
            .Where(t => t.IsPublic && string.Equals(t.Namespace, "Dexpace.Sdk.Core.Auth", StringComparison.Ordinal));

        var offenders = Offenders(types);

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scanner_can_fail()
    {
        Assert.Equal([typeof(LeakyFixture).Name], Offenders([typeof(CompliantFixture), typeof(LeakyFixture)]));
    }

    private static List<string> Offenders(IEnumerable<Type> types)
    {
        var secretNames = new[] { "Token", "Key", "Password", "Secret" };
        var offenders = new List<string>();
        foreach (var type in types)
        {
            var holdsSecret = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => secretNames.Contains(p.Name, StringComparer.Ordinal) && p.PropertyType == typeof(string));
            if (!holdsSecret)
            {
                continue;
            }

            var toString = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (toString is null || toString.DeclaringType != type)
            {
                offenders.Add(type.Name);
            }
        }

        return offenders;
    }

    private sealed record Holder(BasicCredential C);

    private sealed class FixedTokenCredential : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken(Secret));
    }

    private sealed class CompliantFixture
    {
        public string Password { get; } = "x";

        public override string ToString() => "***";
    }

    private sealed class LeakyFixture
    {
        public string Password { get; } = "x";
    }
}
