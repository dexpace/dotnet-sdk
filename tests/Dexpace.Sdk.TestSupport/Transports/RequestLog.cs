// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>The thread-safe call record the fake transports share; a retry test may call from several threads.</summary>
internal sealed class RequestLog
{
    private readonly List<RecordedCall> _calls = [];
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    public RecordedCall? Last
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count > 0 ? _calls[^1] : null;
            }
        }
    }

    /// <summary>Records the call and returns its zero-based call index.</summary>
    public int Add(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _calls.Add(new RecordedCall(request, options, cancellationToken));
            return _calls.Count - 1;
        }
    }

    public IReadOnlyList<RecordedCall> Snapshot()
    {
        lock (_gate)
        {
            return [.. _calls];
        }
    }
}
