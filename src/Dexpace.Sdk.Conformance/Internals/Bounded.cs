// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The kit's bounds (suite-contract clause 6): every assertion, and every wait inside one, ends within a stated time, and
/// a hang is a <see cref="ConformanceException"/> naming the bound, never a hung run. Bounds are real time on purpose: the
/// transports under test talk to a real socket.
/// </summary>
internal static class Bounded
{
    /// <summary>
    /// Runs <paramref name="body"/> under <paramref name="bound"/>. The token handed to the body fires when the bound
    /// passes, and a body that ignores it is abandoned (its later failure is observed, not rethrown).
    /// </summary>
    /// <param name="body">The work, given a token that fires when the bound passes or <paramref name="outer"/> fires.</param>
    /// <param name="bound">The ceiling.</param>
    /// <param name="what">What is bounded, named in the failure, for example the assertion's name.</param>
    /// <param name="outer">The caller's token; its cancellation propagates as <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ConformanceException">The bound passed first.</exception>
    internal static async Task RunAsync(Func<CancellationToken, Task> body, TimeSpan bound, string what, CancellationToken outer)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(outer);
        var work = body(linked.Token);
        try
        {
            await work.WaitAsync(bound, outer).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await linked.CancelAsync().ConfigureAwait(false);
            _ = work.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw Exceeded(what, bound);
        }
    }

    /// <summary>Awaits <paramref name="task"/> for at most <paramref name="bound"/>; a task that is still running then is a failure naming <paramref name="what"/>.</summary>
    /// <param name="task">A non-generic task.</param>
    /// <param name="bound">The ceiling.</param>
    /// <param name="what">What was being waited for, named in the failure.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <exception cref="ConformanceException">The bound passed first.</exception>
    internal static async Task WaitAsync(Task task, TimeSpan bound, string what, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        try
        {
            await task.WaitAsync(bound, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw Exceeded(what, bound);
        }
    }

    /// <summary>Awaits every task in <paramref name="tasks"/> for at most <paramref name="bound"/>, returning their results in order.</summary>
    /// <param name="tasks">The tasks; a failure of any of them is rethrown.</param>
    /// <param name="bound">The ceiling for all of them together.</param>
    /// <param name="what">What was being waited for, named in the failure.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <exception cref="ConformanceException">The bound passed first.</exception>
    internal static async Task<T[]> WaitForAllAsync<T>(IReadOnlyList<Task<T>> tasks, TimeSpan bound, string what, CancellationToken cancellationToken)
    {
        await WaitAsync(Task.WhenAll(tasks), bound, what, cancellationToken).ConfigureAwait(false);
        var results = new T[tasks.Count];
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = await tasks[i].ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    /// Disposes <paramref name="item"/> on a pool thread, bounded: a disposal that blocks its caller (a blocking transport's
    /// <c>Dispose</c> that waits for an in-flight call) is a failure naming the bound instead of a hung run
    /// (<c>TRANSPORT-16</c>: close is non-blocking). The abandoned disposal completes when whatever it waits on does.
    /// </summary>
    /// <param name="item">What to dispose.</param>
    /// <param name="bound">The ceiling.</param>
    /// <param name="what">What is being disposed, named in the failure.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <exception cref="ConformanceException">The disposal outlived <paramref name="bound"/>.</exception>
    internal static Task DisposeAsync(IAsyncDisposable item, TimeSpan bound, string what, CancellationToken cancellationToken) =>
        WaitAsync(Task.Run(async () => await item.DisposeAsync().ConfigureAwait(false), CancellationToken.None), bound, what, cancellationToken);

    /// <summary>The bound rendered for a message: whole seconds from one second up, otherwise milliseconds.</summary>
    /// <param name="bound">The bound.</param>
    internal static string Format(TimeSpan bound) =>
        bound >= TimeSpan.FromSeconds(1)
            ? string.Create(CultureInfo.InvariantCulture, $"{bound.TotalSeconds:0.##} s")
            : string.Create(CultureInfo.InvariantCulture, $"{bound.TotalMilliseconds:0} ms");

    private static ConformanceException Exceeded(string what, TimeSpan bound) =>
        new(
            $"{what} did not finish within its {Format(bound)} bound: a hang is a failure.",
            $"completion within {Format(bound)}",
            "still running");
}
