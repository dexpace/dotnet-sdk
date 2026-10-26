// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// NFR-16's strong-naming half (phase 12, P12-9, P12-10; design §9.2): the five libraries and their friend test assemblies are signed with the key committed
/// as <c>Dexpace.Sdk.snk</c>, and every <c>InternalsVisibleTo</c> grant carries that key's public half through one property, <c>DexpacePublicKey</c>
/// in <c>Directory.Build.props</c>. The compiler already refuses a mismatch (<c>CS1726</c> for a keyless grant to a signed friend, <c>CS0281</c> for a key
/// blob exported as <c>CALG_RSA_KEYX</c>), so these tests pin the three things it cannot: the property is the key the built assemblies carry, it is the
/// <c>CALG_RSA_SIGN</c> blob, and the token the documentation quotes is the key's token (a rotation is therefore a visible edit of this file).
/// </summary>
[Trait("Category", "Unit")]
public sealed class StrongNameKeyTests
{
    /// <summary>The public key token the documentation quotes (the last eight bytes of the SHA-1 of the public key blob, reversed, which is what the runtime derives).</summary>
    private const string DocumentedToken = "fb3fcc3028a3989e";

    private static readonly string[] s_libraries =
    [
        "Dexpace.Sdk.Core",
        "Dexpace.Sdk.Http.SystemNet",
        "Dexpace.Sdk.Serialization.SystemTextJson",
        "Dexpace.Sdk.Extensions.DependencyInjection",
        "Dexpace.Sdk.Conformance",
    ];

    private static readonly Regex s_property = new(@"<DexpacePublicKey>\s*([0-9A-Fa-f]+)\s*</DexpacePublicKey>", RegexOptions.CultureInvariant);
    private static readonly Regex s_grant = new(@"<InternalsVisibleTo\b[^>]*>", RegexOptions.CultureInvariant);

    [Fact]
    public void The_public_key_property_is_the_key_the_assemblies_are_signed_with()
    {
        var expected = PropertyKey(File.ReadAllText(RepositoryRoot.Of("Directory.Build.props")));

        Assert.NotNull(expected);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        foreach (var library in s_libraries)
        {
            var dll = RepositoryRoot.Of($"src/{library}/bin/{configuration}/net10.0/{library}.dll");
            Assert.True(File.Exists(dll), $"{dll} is missing: build the solution in {configuration} first (this test reads each built library, and never skips).");
            Assert.Equal(expected, AssemblyName.GetAssemblyName(dll).GetPublicKey());
        }

        // The friend test assemblies are signed too (V9): a signed library cannot grant internals to an unsigned friend, and a signed friend demands a keyed grant.
        Assert.Equal(expected, typeof(StrongNameKeyTests).Assembly.GetName().GetPublicKey());
    }

    [Fact]
    public void The_key_is_a_signing_key()
    {
        var key = PropertyKey(File.ReadAllText(RepositoryRoot.Of("Directory.Build.props")));

        Assert.NotNull(key);
        Assert.Empty(KeyFlaws(key));

        // The committed private blob (PRIVATEKEYBLOB: 07 02 00 00, then the key algorithm) says the same.
        var snk = File.ReadAllBytes(RepositoryRoot.Of("Dexpace.Sdk.snk"));
        Assert.Equal(new byte[] { 0x07, 0x02, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00 }, snk[..8]);
    }

    [Fact]
    public void The_public_key_token_is_the_documented_one()
    {
        var key = PropertyKey(File.ReadAllText(RepositoryRoot.Of("Directory.Build.props")));

        Assert.NotNull(key);
        Assert.Equal(DocumentedToken, Token(key));
        Assert.Equal(DocumentedToken, Convert.ToHexStringLower(typeof(StrongNameKeyTests).Assembly.GetName().GetPublicKeyToken()!));
    }

    [Fact]
    public void Every_internals_visible_to_item_carries_the_key()
    {
        foreach (var library in s_libraries)
        {
            var project = RepositoryRoot.Of($"src/{library}/{library}.csproj");
            Assert.Empty(GrantFlaws(File.ReadAllText(project)).Select(flaw => $"{library}: {flaw}"));
        }
    }

    [Fact]
    public void The_scan_can_fail()
    {
        // A project with a keyless grant, one with the key on only one of two grants, and one with no grant at all are flagged; a keyed one is not.
        Assert.Single(GrantFlaws("""<ItemGroup><InternalsVisibleTo Include="X.Tests" /></ItemGroup>"""));
        Assert.Single(GrantFlaws("""<InternalsVisibleTo Include="A" Key="$(DexpacePublicKey)" /><InternalsVisibleTo Include="B" />"""));
        Assert.Single(GrantFlaws("<ItemGroup />"));
        Assert.Empty(GrantFlaws("""<InternalsVisibleTo Include="A" Key="$(DexpacePublicKey)" />"""));

        // The CALG_RSA_KEYX trap (00 A4 00 00, CS0281), a short blob and a missing property are flagged; the signing algorithm is not.
        var good = "0024000004800000" + new string('0', 16);
        Assert.Empty(KeyFlaws(Convert.FromHexString(good)));
        Assert.NotEmpty(KeyFlaws(Convert.FromHexString("00A4000004800000" + new string('0', 16))));
        Assert.NotEmpty(KeyFlaws(Convert.FromHexString("0024")));
        Assert.Null(PropertyKey("<Project />"));
        Assert.Equal(Convert.FromHexString(good), PropertyKey($"<DexpacePublicKey>{good}</DexpacePublicKey>"));
    }

    private static byte[]? PropertyKey(string props) =>
        s_property.Match(props) is { Success: true } match ? Convert.FromHexString(match.Groups[1].Value) : null;

    private static List<string> KeyFlaws(byte[] key)
    {
        var flaws = new List<string>();
        if (key.Length < 12)
        {
            flaws.Add($"the key is {key.Length} bytes, shorter than the 12-byte public key header");
        }
        else if (!key.AsSpan(0, 4).SequenceEqual(new byte[] { 0x00, 0x24, 0x00, 0x00 }))
        {
            flaws.Add($"the key algorithm is {Convert.ToHexString(key.AsSpan(0, 4))}, not 00240000 (CALG_RSA_SIGN); CALG_RSA_KEYX (00A40000) gives CS0281");
        }

        return flaws;
    }

    private static List<string> GrantFlaws(string project)
    {
        var grants = s_grant.Matches(project).Select(static m => m.Value).ToList();
        var flaws = grants
            .Where(static grant => !grant.Contains("Key=\"$(DexpacePublicKey)\"", StringComparison.Ordinal))
            .Select(static grant => $"{grant} carries no Key=\"$(DexpacePublicKey)\" (CS1726 against a signed friend)")
            .ToList();
        if (grants.Count == 0)
        {
            flaws.Add("the project grants no InternalsVisibleTo item");
        }

        return flaws;
    }

    // The runtime derives the token itself (SHA-1 of the blob, last eight bytes reversed), so the test needs no hash call of its own (CA5350).
    private static string Token(byte[] publicKey)
    {
        var name = new AssemblyName("Token");
        name.SetPublicKey(publicKey);
        return Convert.ToHexStringLower(name.GetPublicKeyToken()!);
    }
}
