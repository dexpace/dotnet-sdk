// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// The call-scoped half of a <see cref="PipelineContext"/>: one instance per call, shared by reference with every copy
/// the retry and redirect policies make (PIPE-10, PIPE-16, PIPE-17).
/// </summary>
internal sealed class CallState
{
    private readonly Lock _gate = new();
    private Dictionary<object, object?>? _properties;
    private CallContext? _furthest;

    internal CallState(
        Request seedRequest,
        DexpaceClientOptions options,
        RequestOptions requestOptions,
        DispatchContext dispatch,
        ILogger logger)
    {
        Logger = logger;
        SeedRequest = seedRequest;
        Options = options;
        RequestOptions = requestOptions;
        Dispatch = dispatch;
    }

    internal Request SeedRequest { get; }

    internal DexpaceClientOptions Options { get; }

    internal RequestOptions RequestOptions { get; }

    internal DispatchContext Dispatch { get; }

    // The pipeline's logger: its Diagnostics pillar's, else NullLogger (P5b-6). Retry, redirect and the response wrappers
    // report through it; 6a/6b/6c emit their events through it.
    internal ILogger Logger { get; }

    internal bool TryGet<T>(PipelinePropertyKey<T> key, out T value)
    {
        lock (_gate)
        {
            if (_properties is not null && _properties.TryGetValue(key, out var stored))
            {
                if (stored is T typed)
                {
                    value = typed;
                    return true;
                }

                // A null stored under a nullable T is present-with-null, not absent.
                if (stored is null && default(T) is null)
                {
                    value = default!;
                    return true;
                }
            }
        }

        value = default!;
        return false;
    }

    internal void Set<T>(PipelinePropertyKey<T> key, T value)
    {
        lock (_gate)
        {
            (_properties ??= new Dictionary<object, object?>(ReferenceEqualityComparer.Instance))[key] = value;
        }
    }

    // The terminal is the only writer, and a call's drives are sequential.
    internal void Reached(CallContext link)
    {
        lock (_gate)
        {
            _furthest = link;
        }
    }

    // Closes the furthest link reached; never throws (CTX-10). Idempotent.
    internal void CloseFurthest()
    {
        CallContext? link;
        lock (_gate)
        {
            link = _furthest;
            _furthest = null;
        }

        link?.Close();
    }
}
