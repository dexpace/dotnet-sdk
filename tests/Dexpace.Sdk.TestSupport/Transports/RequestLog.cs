// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>The thread-safe request record the fake transports share; a retry test may call from several threads.</summary>
internal sealed class RequestLog
{
    private readonly List<Request> _requests = [];
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count;
            }
        }
    }

    public Request? Last
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count > 0 ? _requests[^1] : null;
            }
        }
    }

    /// <summary>Records <paramref name="request"/> and returns its zero-based call index.</summary>
    public int Add(Request request)
    {
        lock (_gate)
        {
            _requests.Add(request);
            return _requests.Count - 1;
        }
    }

    public IReadOnlyList<Request> Snapshot()
    {
        lock (_gate)
        {
            return [.. _requests];
        }
    }
}
