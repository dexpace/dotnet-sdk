// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>The call's transmission counter behind <c>http.request.resend_count</c> (P5c-8).</summary>
[Trait("Category", "Unit")]
public sealed class CallStateTransmissionTests
{
    [Fact]
    public void The_first_transmission_is_ordinal_zero_and_each_next_is_one_more()
    {
        var state = TestContexts.For().State;

        Assert.Equal([0, 1, 2], new[] { state.NextTransmission(), state.NextTransmission(), state.NextTransmission() });
    }

    [Fact]
    public async Task Concurrent_increments_are_never_lost_or_duplicated()
    {
        var state = TestContexts.For().State;

        var tasks = Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            var seen = new List<int>();
            for (var i = 0; i < 100; i++)
            {
                seen.Add(state.NextTransmission());
            }

            return seen;
        }, TestContext.Current.CancellationToken));
        var all = (await Task.WhenAll(tasks)).SelectMany(x => x).Order().ToArray();

        Assert.Equal(Enumerable.Range(0, 6400), all);
    }

    [Fact]
    public void Transmissions_reads_the_count_so_far()
    {
        var state = TestContexts.For().State;
        Assert.Equal(0, state.Transmissions);

        state.NextTransmission();
        state.NextTransmission();

        Assert.Equal(2, state.Transmissions);
    }

    [Fact]
    public void A_context_copy_shares_the_counter()
    {
        var context = TestContexts.For();
        context.State.NextTransmission();

        var copy = context.ForAttempt(1);

        Assert.Same(context.State, copy.State);
        Assert.Equal(1, copy.State.NextTransmission());
    }
}
