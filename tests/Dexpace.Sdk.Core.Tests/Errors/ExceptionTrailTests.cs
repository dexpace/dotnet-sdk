// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/suppress.test.ts.
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.TestSupport.Recovery;
using Xunit;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).

namespace Dexpace.Sdk.Core.Tests.Exceptions;

[Trait("Category", "Unit")]
public sealed class ExceptionTrailTests
{
    [Fact]
    public void A_secondary_added_to_an_SdkException_lands_on_Suppressed()
    {
        var primary = new SdkException("primary");
        var secondary = new IOException("secondary");

        ExceptionTrail.AddSuppressed(primary, secondary);

        Assert.Same(secondary, Assert.Single(primary.Suppressed));
        Assert.Same(secondary, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
    }

    [Fact]
    public void A_secondary_added_to_a_foreign_exception_lands_in_GetSuppressed()
    {
        var primary = new InvalidOperationException("primary");
        var secondary = new IOException("secondary");

        ExceptionTrail.AddSuppressed(primary, secondary);

        Assert.Same(secondary, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
    }

    [Fact]
    public void GetSuppressed_of_an_exception_with_no_trail_is_empty()
    {
        Assert.Empty(ExceptionTrail.GetSuppressed(new InvalidOperationException()));
        Assert.Empty(ExceptionTrail.GetSuppressed(new SdkException()));
        Assert.Empty(new SdkException().Suppressed);
    }

    [Fact]
    public void AddSuppressed_to_itself_is_a_no_op()
    {
        var sdk = new SdkException("sdk");
        var foreign = new InvalidOperationException("foreign");

        ExceptionTrail.AddSuppressed(sdk, sdk);
        ExceptionTrail.AddSuppressed(foreign, foreign);

        Assert.Empty(sdk.Suppressed);
        Assert.Empty(ExceptionTrail.GetSuppressed(foreign));
    }

    [Fact]
    public void A_duplicate_secondary_is_added_once()
    {
        var primary = new SdkException("primary");
        var foreign = new InvalidOperationException("foreign");
        var secondary = new IOException("secondary");

        ExceptionTrail.AddSuppressed(primary, secondary);
        ExceptionTrail.AddSuppressed(primary, secondary);
        ExceptionTrail.AddSuppressed(foreign, secondary);
        ExceptionTrail.AddSuppressed(foreign, secondary);

        Assert.Single(primary.Suppressed);
        Assert.Single(ExceptionTrail.GetSuppressed(foreign));
    }

    [Fact]
    public void A_fatal_primary_gets_no_attachment()
    {
        var primary = new OutOfMemoryException();

        ExceptionTrail.AddSuppressed(primary, new IOException("secondary"));

        Assert.Empty(ExceptionTrail.GetSuppressed(primary));
    }

    [Fact]
    public void A_fatal_secondary_is_ignored()
    {
        var primary = new SdkException("primary");

        ExceptionTrail.AddSuppressed(primary, new OutOfMemoryException());

        Assert.Empty(primary.Suppressed);
    }

    [Fact]
    public void A_read_only_Data_drops_the_secondary_and_does_not_throw()
    {
        var primary = new ReadOnlyDataException();

        ExceptionTrail.AddSuppressed(primary, new IOException("secondary"));

        Assert.Empty(ExceptionTrail.GetSuppressed(primary));
    }

    [Fact]
    public void A_snapshot_taken_before_an_attach_is_unchanged_after_it()
    {
        var primary = new SdkException("primary");
        var first = new IOException("first");
        ExceptionTrail.AddSuppressed(primary, first);
        var snapshot = primary.Suppressed;

        ExceptionTrail.AddSuppressed(primary, new TimeoutException("second"));

        Assert.Same(first, Assert.Single(snapshot));
        Assert.Equal(2, primary.Suppressed.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_attaches_from_16_tasks_all_land(bool sdkPrimary)
    {
        Exception primary = sdkPrimary ? new SdkException("primary") : new InvalidOperationException("primary");
        var secondaries = Enumerable.Range(0, 16).Select(i => new IOException("s" + i)).ToList();

        await Task.WhenAll(secondaries.Select(s => Task.Run(() => ExceptionTrail.AddSuppressed(primary, s))));

        var trail = ExceptionTrail.GetSuppressed(primary);
        Assert.Equal(16, trail.Count);
        Assert.All(secondaries, s => Assert.Contains(s, trail));
    }

    [Fact]
    public void Argument_null_checks()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionTrail.AddSuppressed(null!, new IOException()));
        Assert.Throws<ArgumentNullException>(() => ExceptionTrail.AddSuppressed(new IOException(), null!));
        Assert.Throws<ArgumentNullException>(() => ExceptionTrail.GetSuppressed(null!));
    }

    [Fact]
    public void SdkException_ToString_renders_each_suppressed_exception_as_a_numbered_block()
    {
        var primary = new SdkException("primary");
        ExceptionTrail.AddSuppressed(primary, new IOException("first-secondary"));
        ExceptionTrail.AddSuppressed(primary, new TimeoutException("second-secondary"));

        var text = primary.ToString();

        Assert.StartsWith("Dexpace.Sdk.Core.Errors.SdkException: primary", text, StringComparison.Ordinal);
        Assert.Contains("(Suppressed Exception #0)", text, StringComparison.Ordinal);
        Assert.Contains("(Suppressed Exception #1)", text, StringComparison.Ordinal);
        Assert.Contains("first-secondary", text, StringComparison.Ordinal);
        Assert.Contains("second-secondary", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mutual_two_exception_trail_renders_cycle_and_terminates()
    {
        var a = new SdkException("a");
        var b = new SdkException("b");
        ExceptionTrail.AddSuppressed(a, b);
        ExceptionTrail.AddSuppressed(b, a);

        var text = a.ToString();

        Assert.Contains("(cycle)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nesting_is_capped_at_8_levels()
    {
        var root = new SdkException("level0");
        var current = root;
        for (var i = 1; i < 20; i++)
        {
            var next = new SdkException("level" + i);
            ExceptionTrail.AddSuppressed(current, next);
            current = next;
        }

        var text = root.ToString();

        var openings = text.Split("(Suppressed Exception", StringSplitOptions.None).Length - 1;
        Assert.InRange(openings, 1, 8);
    }

    [Fact]
    public void A_foreign_exceptions_ToString_does_not_render_its_trail()
    {
        var primary = new InvalidOperationException("primary");
        ExceptionTrail.AddSuppressed(primary, new IOException("secondary"));

        Assert.DoesNotContain("Suppressed Exception", primary.ToString(), StringComparison.Ordinal);
    }
}
