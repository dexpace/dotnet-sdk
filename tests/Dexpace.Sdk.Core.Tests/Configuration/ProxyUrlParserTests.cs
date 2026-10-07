// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-24, CFG-25; P5a-15 item 3, R6, R7, R9. Vectors: tests/vectors/config/proxy-resolution.json, layer "url".
[Trait("Category", "Unit")]
public sealed class ProxyUrlParserTests
{
    public static TheoryData<string> UrlCases() => ProxyResolutionCase.Names("url");

    [Theory]
    [MemberData(nameof(UrlCases))]
    public void Parses_every_vector(string name)
    {
        var vector = ProxyResolutionCase.Named(name);
        var value = vector.Env["HTTPS_PROXY"];

        var ok = ProxyUrlParser.TryParse(value, out var parsed, out var error);

        if (vector.Expected is { } expected)
        {
            Assert.True(ok, $"{name}: {error}");
            Assert.Equal(ProxyUrlError.None, error);
            Assert.Equal(Enum.Parse<ProxyType>(expected.Type), parsed.Type);
            Assert.Equal(expected.Host, parsed.Host);
            Assert.Equal(expected.Port, parsed.Port);
            Assert.Equal(expected.UserName, parsed.UserName);
            Assert.Equal(expected.Password, parsed.Password);
        }
        else
        {
            Assert.False(ok);
            Assert.Equal(vector.Warning!.Reason, error.ToString().ToLowerInvariant());
        }
    }

    [Fact]
    public void Never_throws_for_arbitrary_text()
    {
        var random = new Random(5141);
        string[] schemes = ["http", "socks5", "ftp", "zz"];
        string[] ports = ["", ":0", ":80", ":443", ":8080", ":65535", ":70000", ":x"];
        const string Alphabet = "abcXYZ019_%.:+~@-[]/?# ";
        for (var i = 0; i < 20_000; i++)
        {
            var user = RandomText(random, Alphabet);
            var host = RandomText(random, Alphabet);
            var text = schemes[random.Next(schemes.Length)] + "://" + (random.Next(2) == 0 ? user + "@" : string.Empty) + host + ports[random.Next(ports.Length)];

            var error = Record.Exception(() => ProxyUrlParser.TryParse(text, out _, out _));

            Assert.Null(error);
        }
    }

    [Fact]
    public void The_port_is_read_from_the_raw_text_not_Uri_Port()
    {
        // Fact 7: new Uri("http://p").Port is 80, so Uri cannot tell an explicit port from an absent one.
        Assert.True(ProxyUrlParser.TryParse("http://p:80", out var explicitPort, out _));
        Assert.Equal(80, explicitPort.Port);
        Assert.False(ProxyUrlParser.TryParse("http://p", out _, out var error));
        Assert.Equal(ProxyUrlError.Port, error);
    }

    private static string RandomText(Random random, string alphabet)
    {
        var length = random.Next(0, 12);
        return string.Create(length, (random, alphabet), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = state.alphabet[state.random.Next(state.alphabet.Length)];
            }
        });
    }
}
