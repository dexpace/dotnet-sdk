// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/config/client-identity-step.test.ts.
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Recovery;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class ClientIdentityStepTests
{
    private static Request Plain() => Request.Get("https://example.test/");

    private static Request WithAgents(params string[] values)
    {
        var builder = new Headers.Builder();
        foreach (var value in values)
        {
            builder.Add("User-Agent", value);
        }

        return Plain().WithHeaders(builder.Build());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Tokens_are_joined_by_one_space_and_trimmed(bool async)
    {
        var step = new ClientIdentityStep(["  sdk/1.0", "(linux)  "]);

        var result = await Run.Apply(step, Plain(), async);

        Assert.Equal("sdk/1.0 (linux)", result.Headers.Get("User-Agent"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_composes_the_line_after_the_first_existing_value_and_keeps_every_other_value(bool async)
    {
        var step = new ClientIdentityStep(["sdk/1.0"]);

        var result = await Run.Apply(step, WithAgents("caller/2", "second/3"), async);

        Assert.Equal(["caller/2 sdk/1.0", "second/3"], result.Headers.GetAll("User-Agent"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Append_with_no_existing_value_sets_the_line_as_the_sole_value(bool async)
    {
        var result = await Run.Apply(new ClientIdentityStep(["sdk/1.0"]), Plain(), async);

        Assert.Equal("sdk/1.0", Assert.Single(result.Headers.GetAll("User-Agent")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_empty_first_existing_value_counts_as_absent_with_no_leading_space(bool async)
    {
        var result = await Run.Apply(new ClientIdentityStep(["sdk/1.0"]), WithAgents(string.Empty, "other/1"), async);

        Assert.Equal(["sdk/1.0", "other/1"], result.Headers.GetAll("User-Agent"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Replace_overwrites_every_existing_value(bool async)
    {
        var step = new ClientIdentityStep(["sdk/1.0"]) { Mode = ClientIdentityMode.Replace };

        var result = await Run.Apply(step, WithAgents("caller/2", "second/3"), async);

        Assert.Equal("sdk/1.0", Assert.Single(result.Headers.GetAll("User-Agent")));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("   ", true)]
    [InlineData("   ", false)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public async Task A_blank_or_whitespace_only_line_is_a_no_op_and_emits_no_header(string? token, bool async)
    {
        var step = new ClientIdentityStep(token is null ? [] : [token]);
        var request = Plain();

        var result = await Run.Apply(step, request, async);

        Assert.Same(request, result);
        Assert.False(result.Headers.Contains("User-Agent"));
    }

    [Fact]
    public void The_default_header_is_User_Agent_and_the_default_mode_is_Append()
    {
        var step = new ClientIdentityStep(["x"]);

        Assert.Equal(HttpHeaderName.WellKnown.UserAgent, step.HeaderName);
        Assert.Equal(ClientIdentityMode.Append, step.Mode);
    }

    [Fact]
    public void Tokens_are_copied_at_construction()
    {
        var source = new List<string> { "a" };
        var step = new ClientIdentityStep(source);

        source.Add("b");

        Assert.Equal(["a"], step.Tokens);
        Assert.Equal("a", step.Apply(Plain(), TestContext.Current.CancellationToken).Headers.Get("User-Agent"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_custom_header_name_is_honoured(bool async)
    {
        var step = new ClientIdentityStep(["sdk/1.0"]) { HeaderName = HttpHeaderName.Of("X-Client") };

        var result = await Run.Apply(step, Plain(), async);

        Assert.Equal("sdk/1.0", result.Headers.Get("X-Client"));
        Assert.False(result.Headers.Contains("User-Agent"));
    }

    [Fact]
    public void The_internal_compose_seam_takes_a_token_line()
    {
        var step = new ClientIdentityStep([]);

        var result = step.Compose(WithAgents("caller/2"), "seam/9");

        Assert.Equal("caller/2 seam/9", result.Headers.Get("User-Agent"));
    }

    [Fact]
    public void A_null_tokens_argument_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ClientIdentityStep(null!));
    }
}
