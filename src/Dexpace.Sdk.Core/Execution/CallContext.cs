// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Execution;

/// <summary>
/// The closed base of the three call-context stages: <see cref="DispatchContext"/>, <see cref="RequestContext"/> and
/// <see cref="ExchangeContext"/> (CTX-1, CTX-2, CTX-7, CTX-9, CTX-10, CTX-17, CTX-18).
/// </summary>
/// <remarks>
/// <para>
/// Contexts are immutable records. Promotion returns a new link that carries the same <see cref="Key"/> and the same
/// <see cref="Instrumentation"/> reference, and registers itself in the store the chain is bound to (CTX-17); the
/// pipeline never calls the store. The hierarchy is closed to other assemblies by internal abstract members (P4a-5).
/// </para>
/// <para>
/// A context is deliberately not <see cref="IDisposable"/> (P4a-7): <c>using var dispatch = ...</c> would close the head
/// link, a no-op once promoted, and leave the terminal link registered. Call <see cref="Close"/> on the furthest link.
/// Records bound to different stores are unequal, because equality includes the store.
/// </para>
/// </remarks>
public abstract record CallContext
{
    private readonly ContextStore _store;

    private protected CallContext(InstrumentationContext? instrumentation, CallKey? key, ContextStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        instrumentation ??= InstrumentationContext.None;
        if (key is { } explicitKey && explicitKey == default)
        {
            throw new ArgumentException("A default(CallKey) was never minted; mint one with CallKey.Next.", nameof(key));
        }

        _store = store;
        Instrumentation = instrumentation;
        Key = key ?? CallKey.Next(instrumentation.ActivityContext);
    }

    /// <summary>The key shared by every link of this call's chain.</summary>
    public CallKey Key { get; }

    /// <summary>The correlation bundle, carried by reference through every promotion.</summary>
    public InstrumentationContext Instrumentation { get; }

    private protected ContextStore Store => _store;

    /// <summary>The flavour name rendered by <see cref="ToString"/>; internal and abstract to close the hierarchy.</summary>
    internal abstract string StageName { get; }

    /// <summary>
    /// Evicts this context from the store if, and only if, it is the registered occupant of its key. A context that was
    /// replaced by its successor, a value-equal clone, an unregistered or an already closed context is a no-op. Never
    /// throws and does not dispose the <c>Response</c> it may carry (CTX-9, CTX-10, CTX-18).
    /// </summary>
    public void Close() => _ = _store.Release(this);

    /// <summary>
    /// Renders the stage, the key and the stage's detail; never the headers, bodies or the request graph (P4a-14).
    /// </summary>
    /// <returns>The rendering.</returns>
    public sealed override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(StageName).Append('[').Append(Key).Append(']');
        AppendDetail(builder);
        return builder.ToString();
    }

    /// <summary>Appends the stage's detail; internal and abstract to close the hierarchy.</summary>
    /// <param name="builder">The builder.</param>
    internal abstract void AppendDetail(StringBuilder builder);
}
