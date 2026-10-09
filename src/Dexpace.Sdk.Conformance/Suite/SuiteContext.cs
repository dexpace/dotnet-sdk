// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// What one assertion run owns: the servers it started, the transports it built, the logger the subject's transport logs
/// to, and the hooks it asked for. Everything is created for this run and disposed after it, on every path (suite-contract
/// clauses 1 and 8: a fresh transport and a fresh server per assertion, so a poisoned connection cannot fail a later row).
/// </summary>
internal sealed class SuiteContext(TransportSubject subject, TransportFace face, TransportSuiteOptions options) : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _transports = [];
    private readonly List<IAsyncDisposable> _servers = [];
    private readonly List<IAsyncDisposable> _others = [];
    private readonly object _lock = new();

    /// <summary>The subject under test.</summary>
    internal TransportSubject Subject { get; } = subject;

    /// <summary>The face this run drives.</summary>
    internal TransportFace Face { get; } = face;

    /// <summary>The run's options.</summary>
    internal TransportSuiteOptions Options { get; } = options;

    /// <summary>The logger every transport built for this run logs to.</summary>
    internal RecordingLogger Logger { get; } = new();

    /// <summary>The settings handed to every factory.</summary>
    internal TransportSettings Settings => new() { Logger = Logger };

    /// <summary>The bound on observing a release (and the window of the one timed negative).</summary>
    internal TimeSpan ReleaseTimeout => Options.ReleaseTimeout;

    /// <summary>Whether <paramref name="subject"/> supplies a factory for <paramref name="face"/>.</summary>
    internal static bool Supplies(TransportSubject subject, TransportFace face) =>
        face == TransportFace.Async ? subject.CreateAsync is not null : subject.CreateBlocking is not null;

#pragma warning disable CA2000 // Ownership passes to the context's tracking lists, which DisposeAsync drains on every path.
    /// <summary>Starts a loopback server answering request <c>n</c> with entry <c>n</c> of <paramref name="script"/>; disposed with the context.</summary>
    internal LoopbackServer StartServer(params LoopbackResponse[] script) => Track(_servers, LoopbackServer.Start(script));

    /// <summary>Starts a loopback server that builds each reply from the request; disposed with the context.</summary>
    internal LoopbackServer StartServer(Func<RecordedRequest, LoopbackResponse> respond) => Track(_servers, LoopbackServer.Start(respond));

    /// <summary>Builds the subject's transport for this run's face, wrapped as the face's send primitive; disposed with the context.</summary>
    internal IFaceTransport CreateTransport() => Track(_transports, Face == TransportFace.Async
        ? new AsyncFaceTransport(Subject.CreateAsync!(Settings))
        : (IFaceTransport)new BlockingFaceTransport(Subject.CreateBlocking!(Settings)));

    /// <summary>Builds the subject's asynchronous transport directly, unwrapped, for an assertion that calls the interface itself.</summary>
    internal AsyncFaceTransport CreateAsyncTransport() => Track(_transports, new AsyncFaceTransport(
        (Subject.CreateAsync ?? throw new NotExercisedException("the subject supplies no Async factory (CreateAsync)"))(Settings)));

    /// <summary>Wraps a transport a hook built as an async primitive; disposed with the context.</summary>
    internal AsyncFaceTransport Adopt(IAsyncHttpClient transport) => Track(_transports, new AsyncFaceTransport(transport));

#pragma warning restore CA2000

    /// <summary>Hands <paramref name="disposable"/> to the context, which disposes it after the run.</summary>
    internal T Own<T>(T disposable)
        where T : IAsyncDisposable => Track(_others, disposable);

    /// <summary>The <see cref="TransportSubject.CreateBorrowed"/> hook's result, or <see cref="NotExercisedException"/> naming the hook.</summary>
    internal BorrowedTransport RequireBorrowed()
    {
        var borrowed = Hook(Subject.CreateBorrowed, nameof(TransportSubject.CreateBorrowed))(Settings);
        Track(_transports, borrowed.Transport);
        return Track(_others, borrowed);
    }

    /// <summary>The <see cref="TransportSubject.CreateWithFaultingAdaptation"/> hook's transport, wrapped as an async primitive.</summary>
    internal AsyncFaceTransport RequireFaultingAdaptation() =>
        Adopt(Hook(Subject.CreateWithFaultingAdaptation, nameof(TransportSubject.CreateWithFaultingAdaptation))(Settings));

    /// <summary>The <see cref="TransportSubject.CreateWithInternalCancel"/> hook's result; its transport is disposed with the context.</summary>
    internal InternalCancellation RequireInternalCancel()
    {
        var internalCancel = Hook(Subject.CreateWithInternalCancel, nameof(TransportSubject.CreateWithInternalCancel))(Settings);
        Track(_transports, internalCancel.Transport);
        return internalCancel;
    }

    /// <summary>The <see cref="TransportSubject.CreateWithNativeResend"/> hook's transport, wrapped as an async primitive.</summary>
    internal AsyncFaceTransport RequireNativeResend() =>
        Adopt(Hook(Subject.CreateWithNativeResend, nameof(TransportSubject.CreateWithNativeResend))(Settings));

    /// <summary>The <see cref="TransportSubject.CreateWithProxy"/> hook's transport for <paramref name="proxy"/>, wrapped as an async primitive.</summary>
    /// <param name="proxy">The proxy to install.</param>
    internal AsyncFaceTransport RequireProxy(ProxyOptions proxy) =>
        Adopt(Hook(Subject.CreateWithProxy, nameof(TransportSubject.CreateWithProxy))(Settings, proxy));

    /// <summary>
    /// Disposes everything the run owns: transports first, so they release their connections, then servers, then the rest.
    /// Every item is disposed even when an earlier one throws; a transport whose disposal throws is a
    /// <see cref="ConformanceException"/> (<c>TRANSPORT-16</c>), anything else is rethrown as it is.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Exception? transportFailure = null;
        Exception? otherFailure = null;
        foreach (var (group, isTransport) in new[] { (_transports, true), (_servers, false), (_others, false) })
        {
            foreach (var item in Drain(group))
            {
                try
                {
                    await item.DisposeAsync().ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Not swallowed: the first failure per kind is rethrown below, after everything was disposed.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    if (isTransport)
                    {
                        transportFailure ??= ex;
                    }
                    else
                    {
                        otherFailure ??= ex;
                    }
                }
            }
        }

        if (transportFailure is not null)
        {
            throw new ConformanceException(
                $"Disposing the transport threw {transportFailure.GetType().Name} (TRANSPORT-16: close is idempotent and does not throw).",
                transportFailure);
        }

        if (otherFailure is not null)
        {
            throw otherFailure;
        }
    }

    private static T Hook<T>(T? hook, string name)
        where T : class => hook ?? throw new NotExercisedException($"the subject supplies no {name} hook");

    private T Track<T>(List<IAsyncDisposable> group, T item)
        where T : IAsyncDisposable
    {
        lock (_lock)
        {
            group.Add(item);
        }

        return item;
    }

    private IAsyncDisposable[] Drain(List<IAsyncDisposable> group)
    {
        lock (_lock)
        {
            var items = group.AsEnumerable().Reverse().ToArray();
            group.Clear();
            return items;
        }
    }
}
