// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// Builds an <see cref="HttpPipeline"/> from an ordered set of <see cref="HttpPipelinePolicy"/>
/// instances and a terminal transport.
/// </summary>
/// <remarks>
/// <para>
/// The builder records each policy with the stage it reported <b>once</b>, at insertion, and never reads
/// <see cref="HttpPipelinePolicy.Stage"/> again (PIPE-22). <see cref="Build()"/> stable-sorts the entries by that stage
/// (PIPE-1), so cross-stage order is the enum's numeric order whatever the insertion order, and order within a
/// non-pillar stage is insertion order (PIPE-7).
/// </para>
/// <para>
/// <b>Pillars.</b> A pillar stage admits one policy. A distinct second policy for an occupied pillar throws
/// <see cref="InvalidOperationException"/> at the call that adds it (PIPE-5), naming both types and
/// <c>Replace&lt;T&gt;</c>; re-adding the same instance is a no-op, decided by reference identity (PIPE-6).
/// </para>
/// <para>
/// <b>Breaking:</b> a pillar collision threw at <c>Build</c>, naming a count; it now throws at <c>Add</c> naming both
/// types, and re-adding the same instance, which used to throw, is a no-op.
/// </para>
/// <para>
/// <b>Breaking:</b> a cross-stage <c>InsertBefore</c>/<c>InsertAfter</c> silently re-bucketed the policy and a
/// cross-stage <c>Replace</c> was accepted; both now throw <see cref="ArgumentException"/> (PIPE-18, PIPE-19).
/// </para>
/// </remarks>
public sealed class PipelineBuilder
{
    private readonly List<PipelineEntry> _entries = [];
    private IAsyncHttpClient? _transport;
    private DexpaceClientOptions? _options;

    internal IReadOnlyList<PipelineEntry> Entries => _entries;

    /// <summary>
    /// Appends <paramref name="policy"/> to the tail of its stage. Re-adding the same instance to a pillar is a no-op;
    /// re-adding it to a non-pillar stage appends it again.
    /// </summary>
    /// <param name="policy">The policy to add.</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="ArgumentException">The policy reports a stage that is not a <see cref="PipelineStage"/> member.</exception>
    /// <exception cref="InvalidOperationException">A different policy already occupies the policy's pillar (PIPE-5).</exception>
    public PipelineBuilder Add(HttpPipelinePolicy policy)
    {
        var entry = MakeEntry(policy);
        if (Admit(_entries, entry))
        {
            _entries.Add(entry);
        }

        return this;
    }

    /// <summary>
    /// Puts <paramref name="policy"/> at the head of its stage (PIPE-7).
    /// </summary>
    /// <param name="policy">The policy to prepend.</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="ArgumentException">The policy reports an undefined stage.</exception>
    /// <exception cref="InvalidOperationException">A different policy already occupies the policy's pillar (PIPE-5).</exception>
    public PipelineBuilder Prepend(HttpPipelinePolicy policy)
    {
        var entry = MakeEntry(policy);
        if (Admit(_entries, entry))
        {
            PrependTo(_entries, entry);
        }

        return this;
    }

    /// <summary>
    /// Adds every policy in <paramref name="policies"/>, keeping the batch's order within each stage (PIPE-38). The batch
    /// is validated against the builder and against itself before anything is committed, so a collision leaves the
    /// builder unchanged (PIPE-23).
    /// </summary>
    /// <param name="policies">The policies to add.</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="InvalidOperationException">A policy collides on a pillar; nothing is added.</exception>
    public PipelineBuilder AddRange(IEnumerable<HttpPipelinePolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var scratch = new List<PipelineEntry>(_entries);
        foreach (var policy in policies)
        {
            var entry = MakeEntry(policy);
            if (Admit(scratch, entry))
            {
                scratch.Add(entry);
            }
        }

        Commit(scratch);
        return this;
    }

    /// <summary>
    /// Prepends each policy in <paramref name="policies"/> in turn, so the batch ends up <b>reversed</b> within each
    /// stage (PIPE-38). All-or-nothing, as <see cref="AddRange"/> (PIPE-23).
    /// </summary>
    /// <param name="policies">The policies to prepend.</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="InvalidOperationException">A policy collides on a pillar; nothing is added.</exception>
    public PipelineBuilder PrependRange(IEnumerable<HttpPipelinePolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var scratch = new List<PipelineEntry>(_entries);
        foreach (var policy in policies)
        {
            var entry = MakeEntry(policy);
            if (Admit(scratch, entry))
            {
                PrependTo(scratch, entry);
            }
        }

        Commit(scratch);
        return this;
    }

    /// <summary>
    /// Inserts <paramref name="policy"/> immediately before the first policy of runtime type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to search for.</typeparam>
    /// <param name="policy">The policy to insert; it must report the anchor's stage (PIPE-18).</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="InvalidOperationException">No policy of type <typeparamref name="T"/> is present (PIPE-21).</exception>
    /// <exception cref="ArgumentException">The policy's stage differs from the anchor's, naming both.</exception>
    public PipelineBuilder InsertBefore<T>(HttpPipelinePolicy policy)
        where T : HttpPipelinePolicy
    {
        var entry = MakeEntry(policy);
        var index = FindFirst<T>();
        RequireSameStage(entry, _entries[index], nameof(policy));
        if (Admit(_entries, entry))
        {
            _entries.Insert(index, entry);
        }

        return this;
    }

    /// <summary>
    /// Inserts <paramref name="policy"/> immediately after the first policy of runtime type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to search for.</typeparam>
    /// <param name="policy">The policy to insert; it must report the anchor's stage (PIPE-18).</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="InvalidOperationException">No policy of type <typeparamref name="T"/> is present (PIPE-21).</exception>
    /// <exception cref="ArgumentException">The policy's stage differs from the anchor's, naming both.</exception>
    public PipelineBuilder InsertAfter<T>(HttpPipelinePolicy policy)
        where T : HttpPipelinePolicy
    {
        var entry = MakeEntry(policy);
        var index = FindFirst<T>();
        RequireSameStage(entry, _entries[index], nameof(policy));
        if (Admit(_entries, entry))
        {
            _entries.Insert(index + 1, entry);
        }

        return this;
    }

    /// <summary>
    /// Replaces the first policy of runtime type <typeparamref name="T"/> with <paramref name="policy"/>. A pillar
    /// replacement never collides: it is one-for-one within the stage.
    /// </summary>
    /// <typeparam name="T">The type to replace.</typeparam>
    /// <param name="policy">The replacement policy; it must report the replaced policy's stage (PIPE-19).</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="InvalidOperationException">No policy of type <typeparamref name="T"/> is present (PIPE-21).</exception>
    /// <exception cref="ArgumentException">The policy's stage differs from the replaced policy's, naming both.</exception>
    public PipelineBuilder Replace<T>(HttpPipelinePolicy policy)
        where T : HttpPipelinePolicy
    {
        var entry = MakeEntry(policy);
        var index = FindFirst<T>();
        RequireSameStage(entry, _entries[index], nameof(policy));
        _entries[index] = entry;
        return this;
    }

    /// <summary>
    /// Removes every policy of runtime type <typeparamref name="T"/>, keeping the rest in order. A no-op when none is
    /// present (PIPE-20).
    /// </summary>
    /// <typeparam name="T">The type to remove.</typeparam>
    /// <returns>This builder (fluent interface).</returns>
    public PipelineBuilder Remove<T>()
        where T : HttpPipelinePolicy
    {
        _entries.RemoveAll(e => e.Policy is T);
        return this;
    }

    /// <summary>
    /// Installs the standard resilience pillars, <see cref="OperationPolicy"/>, <see cref="RedirectPolicy"/>,
    /// <see cref="RetryPolicy"/> and <see cref="InstrumentationPolicy"/>, into EMPTY pillars only (PIPE-24). All four are
    /// checked up front; if any target pillar is occupied nothing is installed.
    /// </summary>
    /// <param name="timeProvider">The time provider for <see cref="OperationPolicy"/> and <see cref="RetryPolicy"/>, or <see langword="null"/> for the system clock.</param>
    /// <param name="logger">The logger for <see cref="InstrumentationPolicy"/>, or <see langword="null"/>.</param>
    /// <returns>This builder (fluent interface).</returns>
    /// <exception cref="InvalidOperationException">A target pillar is occupied; the builder is unchanged.</exception>
    public PipelineBuilder AddStandardResilience(TimeProvider? timeProvider = null, ILogger? logger = null)
    {
        HttpPipelinePolicy[] preset =
        [
            new OperationPolicy(timeProvider),
            new RedirectPolicy(),
            new RetryPolicy(timeProvider),
            new InstrumentationPolicy(logger),
        ];

        foreach (var policy in preset)
        {
            var entry = MakeEntry(policy);
            if (_entries.Any(e => e.Stage == entry.Stage))
            {
                throw new InvalidOperationException(
                    $"Pipeline stage '{entry.Stage}' already holds {_entries.First(e => e.Stage == entry.Stage).Policy.GetType().Name}; "
                    + "the standard resilience preset installs into empty pillars only and installed nothing (PIPE-24).");
            }
        }

        return AddRange(preset);
    }

    /// <summary>
    /// Starts a builder that <b>flattens</b> <paramref name="pipeline"/>: it copies the pipeline's policies, transport and
    /// client options, so policies added afterwards run in the same loops as the inner ones (PIPE-35).
    /// </summary>
    /// <param name="pipeline">The pipeline to flatten.</param>
    /// <returns>A new builder seeded from <paramref name="pipeline"/>.</returns>
    public static PipelineBuilder Flatten(HttpPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var builder = new PipelineBuilder { _transport = pipeline.Transport, _options = pipeline.ClientOptions };
        builder._entries.AddRange(pipeline.Entries);
        return builder;
    }

    /// <summary>
    /// Starts an empty builder whose transport is <paramref name="pipeline"/> itself, so the inner pipeline's loops are
    /// opaque to the outer one: each outer call is one call of the inner pipeline (PIPE-35).
    /// </summary>
    /// <param name="pipeline">The pipeline to nest.</param>
    /// <returns>A new, empty builder over <paramref name="pipeline"/>.</returns>
    public static PipelineBuilder Nest(HttpPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new PipelineBuilder { _transport = pipeline };
    }

    /// <summary>
    /// Builds over the transport this builder was seeded with by <see cref="Flatten"/> or <see cref="Nest"/>.
    /// </summary>
    /// <returns>The pipeline.</returns>
    /// <exception cref="InvalidOperationException">The builder was not seeded with a transport.</exception>
    public HttpPipeline Build() =>
        _transport is null
            ? throw new InvalidOperationException(
                "This builder has no transport: seed it with Flatten or Nest, or call Build(transport).")
            : Build(_transport, _options ?? new DexpaceClientOptions());

    /// <summary>
    /// Builds the pipeline over <paramref name="transport"/>, capturing default client options (or the options a
    /// <see cref="Flatten"/> seed carried).
    /// </summary>
    /// <param name="transport">The terminal transport; the pipeline never owns or disposes it.</param>
    /// <returns>The pipeline.</returns>
    /// <remarks>
    /// <b>Breaking (additive):</b> the pipeline captures client options at build, so
    /// <c>SendAsync(Request, CancellationToken)</c> and the seam entry points have options to run with (P4c-11).
    /// </remarks>
    public HttpPipeline Build(IAsyncHttpClient transport) =>
        Build(transport, _options ?? new DexpaceClientOptions());

    /// <summary>
    /// Builds the pipeline over <paramref name="transport"/>, capturing <paramref name="options"/> as the client options
    /// every call runs with unless a per-call override is passed.
    /// </summary>
    /// <param name="transport">The terminal transport; the pipeline never owns or disposes it.</param>
    /// <param name="options">The client options to capture.</param>
    /// <returns>The pipeline.</returns>
    public HttpPipeline Build(IAsyncHttpClient transport, DexpaceClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);

        // Stable sort by the stage recorded at insertion (fact 5: OrderBy is stable).
        PipelineEntry[] sorted = [.. _entries.OrderBy(e => (int)e.Stage)];
        return new HttpPipeline(sorted, new PipelineTerminal(transport), options);
    }

    private static PipelineEntry MakeEntry(HttpPipelinePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        // The one read of Stage (fact 6), validated against the named members (fact 3).
        var stage = policy.Stage;
        if (!PipelineStageFacts.IsDefinedStage(stage))
        {
            throw new ArgumentException(
                $"The policy {policy.GetType().Name} reports the stage value {(int)stage}, which is not a PipelineStage member.",
                nameof(policy));
        }

        return new PipelineEntry(policy, stage);
    }

    // Returns false when the entry is the same instance already on its pillar (a no-op), throws on a distinct occupant.
    private static bool Admit(List<PipelineEntry> entries, PipelineEntry entry)
    {
        if (!PipelineStageFacts.IsPillar(entry.Stage))
        {
            return true;
        }

        foreach (var existing in entries)
        {
            if (existing.Stage != entry.Stage)
            {
                continue;
            }

            if (ReferenceEquals(existing.Policy, entry.Policy))
            {
                return false;
            }

            throw new InvalidOperationException(
                $"Pipeline stage '{entry.Stage}' already holds {existing.Policy.GetType().Name}; "
                + $"cannot add {entry.Policy.GetType().Name}. Use Replace<{existing.Policy.GetType().Name}>(...) to swap it.");
        }

        return true;
    }

    private static void PrependTo(List<PipelineEntry> entries, PipelineEntry entry)
    {
        var index = entries.FindIndex(e => e.Stage == entry.Stage);
        entries.Insert(index < 0 ? 0 : index, entry);
    }

    private static void RequireSameStage(PipelineEntry entry, PipelineEntry anchor, string paramName)
    {
        if (entry.Stage != anchor.Stage)
        {
            throw new ArgumentException(
                $"The policy {entry.Policy.GetType().Name} is in stage '{entry.Stage}', but {anchor.Policy.GetType().Name} is in "
                + $"stage '{anchor.Stage}'; an edit relative to an anchor must stay within one stage.",
                paramName);
        }
    }

    private void Commit(List<PipelineEntry> scratch)
    {
        _entries.Clear();
        _entries.AddRange(scratch);
    }

    private int FindFirst<T>() where T : HttpPipelinePolicy
    {
        var index = _entries.FindIndex(e => e.Policy is T);
        return index >= 0
            ? index
            : throw new InvalidOperationException($"No policy of type '{typeof(T).Name}' is registered in this builder.");
    }
}
