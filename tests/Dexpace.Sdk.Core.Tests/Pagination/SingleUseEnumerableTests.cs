// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-14 and PAGE-6: the single-use page views, async and blocking (design fact 8).</summary>
[Trait("Category", "Unit")]
public sealed class SingleUseEnumerableTests
{
    private static async IAsyncEnumerable<int> Items([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return 1;
        yield return 2;
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }

    [Fact]
    public async Task The_second_GetAsyncEnumerator_throws_and_the_first_still_works()
    {
        var view = new SingleUseAsyncEnumerable<int>(token => Items(token));
        await using var first = view.GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await first.MoveNextAsync());

        var ex = Assert.Throws<InvalidOperationException>(() => view.GetAsyncEnumerator(TestContext.Current.CancellationToken));

        Assert.Contains("single-use", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, first.Current);
        Assert.True(await first.MoveNextAsync());
        Assert.Equal(2, first.Current);
    }

    [Fact]
    public async Task The_factory_is_not_invoked_until_the_first_GetAsyncEnumerator()
    {
        var calls = 0;
        var view = new SingleUseAsyncEnumerable<int>(token =>
        {
            calls++;
            return Items(token);
        });
        Assert.Equal(0, calls);

        await using var enumerator = view.GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void The_latch_is_race_safe_exactly_one_of_64_callers_wins()
    {
        var view = new SingleUseAsyncEnumerable<int>(token => Items(token));
        var winners = 0;
        var losers = 0;

        Parallel.For(0, 64, new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken }, index =>
        {
            try
            {
                _ = view.GetAsyncEnumerator(CancellationToken.None);
                Interlocked.Increment(ref winners);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref losers);
            }
        });

        Assert.Equal((1, 63), (winners, losers));
    }

    [Fact]
    public async Task The_token_reaches_an_iterator_parameter_marked_EnumeratorCancellation()
    {
        using var cts = new CancellationTokenSource();
        var view = new SingleUseAsyncEnumerable<int>(Items);
        await using var enumerator = view.GetAsyncEnumerator(cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task The_token_reaches_a_factory_that_is_not_an_iterator_and_has_no_EnumeratorCancellation()
    {
        // A subclass's WalkPagesAsync may be a plain method returning a stream that captured the token: the view
        // passes the token to the factory too, so WithCancellation on AsPages() still reaches it (P7c plan 2.3).
        CancellationToken seen = default;
        using var cts = new CancellationTokenSource();
        var view = new SingleUseAsyncEnumerable<int>(token =>
        {
            seen = token;
            return Items(token);
        });

        await using var enumerator = view.GetAsyncEnumerator(cts.Token);

        Assert.Equal(cts.Token, seen);
    }

    [Fact]
    public async Task WithCancellation_on_the_view_flows_to_the_factory()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        var view = new SingleUseAsyncEnumerable<int>(token =>
        {
            seen = token;
            return Items(token);
        });

        await foreach (var _ in view.WithCancellation(cts.Token))
        {
            break;
        }

        Assert.Equal(cts.Token, seen);
    }

    // ── the blocking twin ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_second_GetEnumerator_throws_and_the_first_still_works()
    {
        var view = new SingleUseEnumerable<int>(() => [1, 2]);
        using var first = view.GetEnumerator();
        Assert.True(first.MoveNext());

        var ex = Assert.Throws<InvalidOperationException>(() => view.GetEnumerator());

        Assert.Contains("single-use", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, first.Current);
        Assert.True(first.MoveNext());
        Assert.Equal(2, first.Current);
    }

    [Fact]
    public void The_blocking_factory_is_not_invoked_until_the_first_GetEnumerator()
    {
        var calls = 0;
        var view = new SingleUseEnumerable<int>(() =>
        {
            calls++;
            return [1];
        });
        Assert.Equal(0, calls);

        using var enumerator = view.GetEnumerator();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void The_blocking_latch_is_race_safe_and_the_non_generic_enumerator_shares_it()
    {
        var view = new SingleUseEnumerable<int>(() => [1]);
        var winners = 0;

        Parallel.For(0, 64, new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken }, index =>
        {
            try
            {
                _ = view.GetEnumerator();
                Interlocked.Increment(ref winners);
            }
            catch (InvalidOperationException)
            {
            }
        });

        Assert.Equal(1, winners);
        Assert.Throws<InvalidOperationException>(() => ((System.Collections.IEnumerable)view).GetEnumerator());
    }
}
