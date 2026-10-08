// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Auth;

/// <summary>The count of responses sent for one server nonce (AUTH-18): atomic, so concurrent answers never repeat a count.</summary>
internal sealed class NonceCounter
{
    private int _value;

    /// <summary>The next <c>nc</c>: 1 for the first use of the nonce.</summary>
    internal uint Next() => (uint)Interlocked.Increment(ref _value);

    /// <summary>Test hook: positions the counter so the next use yields <paramref name="next"/>.</summary>
    internal void SetNext(uint next) => Volatile.Write(ref _value, unchecked((int)(next - 1)));
}
