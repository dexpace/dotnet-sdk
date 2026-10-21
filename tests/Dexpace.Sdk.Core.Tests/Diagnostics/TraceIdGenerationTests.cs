// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;
using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-27 (design §10 entry 24, P11-18): the W3C and Datadog flavours of trace-id generation, neither ever all-zero.</summary>
[Trait("Category", "Unit")]
public sealed class TraceIdGenerationTests
{
    private const int Draws = 10_000;

    [Fact]
    public void W3C_ids_are_32_lowercase_hex_and_never_all_zero()
    {
        for (var i = 0; i < Draws; i++)
        {
            var hex = TraceIdGeneration.W3C().ToHexString();

            Assert.Matches("^[0-9a-f]{32}$", hex);
            Assert.NotEqual(new string('0', 32), hex);
        }
    }

    [Fact]
    public void Datadog_ids_have_a_zero_high_half_a_non_zero_low_half_and_render_as_a_decimal_ulong()
    {
        for (var i = 0; i < Draws; i++)
        {
            var id = TraceIdGeneration.Datadog();
            var hex = id.ToHexString();

            Assert.Matches("^[0-9a-f]{32}$", hex);
            Assert.Equal(new string('0', 16), hex[..16]);
            Assert.NotEqual(new string('0', 16), hex[16..]);

            var decimalText = TraceIdGeneration.FormatDatadog(id);
            Assert.Matches("^[1-9][0-9]*$", decimalText);
            Assert.True(ulong.TryParse(decimalText, NumberStyles.None, CultureInfo.InvariantCulture, out var value));
            Assert.Equal(ulong.Parse(hex[16..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture), value);
        }
    }

    [Fact]
    public void FormatDatadog_renders_the_low_half_of_a_known_id_in_decimal()
    {
        var id = ActivityTraceId.CreateFromString("0000000000000000ffffffffffffffff");

        Assert.Equal("18446744073709551615", TraceIdGeneration.FormatDatadog(id));
        Assert.Equal("1", TraceIdGeneration.FormatDatadog(ActivityTraceId.CreateFromString("00000000000000000000000000000001")));
    }

    [Fact]
    public void FormatDatadog_rejects_the_default_id()
    {
        var ex = Assert.Throws<ArgumentException>(() => TraceIdGeneration.FormatDatadog(default));

        Assert.Equal("traceId", ex.ParamName);
    }

    [Fact]
    public void FormatDatadog_rejects_an_id_whose_low_half_is_zero()
    {
        var id = ActivityTraceId.CreateFromString("00000000000000010000000000000000");

        var ex = Assert.Throws<ArgumentException>(() => TraceIdGeneration.FormatDatadog(id));

        Assert.Equal("traceId", ex.ParamName);
    }

    [Fact]
    public void The_W3C_generator_draws_again_after_an_all_zero_draw()
    {
        var calls = 0;

        var id = TraceIdGeneration.W3C(buffer =>
        {
            calls++;
            buffer.Clear();
            if (calls >= 3)
            {
                buffer[15] = 0xAB;
            }
        });

        Assert.Equal(3, calls);
        Assert.Equal("000000000000000000000000000000ab", id.ToHexString());
    }

    [Fact]
    public void The_Datadog_generator_draws_again_after_an_all_zero_draw_and_asks_only_for_the_low_half()
    {
        var calls = 0;
        var lengths = new List<int>();

        var id = TraceIdGeneration.Datadog(buffer =>
        {
            calls++;
            lengths.Add(buffer.Length);
            buffer.Clear();
            if (calls >= 3)
            {
                buffer[0] = 0x01;
            }
        });

        Assert.Equal(3, calls);
        Assert.All(lengths, length => Assert.Equal(8, length));
        Assert.Equal("0000000000000000" + "0100000000000000", id.ToHexString());
        Assert.Equal("72057594037927936", TraceIdGeneration.FormatDatadog(id));
    }

    [Fact]
    public void The_generators_are_assignable_to_the_runtimes_hook_without_the_sdk_setting_it()
    {
        var before = Activity.TraceIdGenerator;
        Func<ActivityTraceId> w3c = TraceIdGeneration.W3C;
        Func<ActivityTraceId> datadog = TraceIdGeneration.Datadog;

        Assert.NotEqual(default, w3c());
        Assert.NotEqual(default, datadog());
        Assert.Same(before, Activity.TraceIdGenerator);
    }
}
