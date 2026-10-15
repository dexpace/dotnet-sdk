// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A <see cref="SynchronizationContext"/> with exactly one thread, the way a UI thread or a legacy request context has one: work
/// posted to it runs there, one item at a time. A library call made on it that blocks the thread on work that needs the thread
/// again never finishes, which is how <c>nfr-11.scheduler-agnostic</c> finds a policy that is not scheduler-agnostic.
/// </summary>
internal sealed class SingleThreadContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
    private readonly Thread _thread;

    /// <summary>Starts the context's thread.</summary>
    internal SingleThreadContext()
    {
        _thread = new Thread(Pump) { IsBackground = true, Name = "single-thread-context" };
        _thread.Start();
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        try
        {
            _queue.Add((d, state));
        }
        catch (InvalidOperationException)
        {
            // The context was disposed: the work is dropped, as a completed UI loop drops it.
        }
    }

    /// <inheritdoc />
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Thread.CurrentThread == _thread)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim();
        Post(_ =>
        {
            d(state);
            done.Set();
        }, null);
        done.Wait();
    }

    /// <summary>Runs <paramref name="work"/> on the context's thread, with the context current, and completes when it does.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="work">The work; its continuations capture this context.</param>
    internal Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async _ =>
        {
            try
            {
#pragma warning disable CA2007 // The point: the work's continuations must capture this context, as a UI caller's do.
                var result = await work();
#pragma warning restore CA2007
#pragma warning disable RS0030 // SEAM-30: this completion carries the assertion's own result (a status code), never a Response that could be orphaned.
                completion.TrySetResult(result);
#pragma warning restore RS0030
            }
#pragma warning disable CA1031 // The failure belongs to the caller's task, not to the pump.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                completion.TrySetException(ex);
            }
        }, null);
        return completion.Task;
    }

    /// <inheritdoc />
    public void Dispose() => _queue.CompleteAdding();

    private void Pump()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}
