// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// One disposable component for <c>XCUT-13</c>: how to close it on each face. The asynchronous assertion calls
/// <see cref="DisposeAsync"/>, the blocking one <see cref="Dispose"/>; both must be latched (a repeat is a no-op) and must not
/// wait for a read parked on the body.
/// </summary>
/// <param name="Kind">What the component is, named in a failure, for example <c>Response</c>.</param>
/// <param name="Dispose">The synchronous close.</param>
/// <param name="DisposeAsync">The asynchronous close.</param>
public sealed record DisposableUnderTest(string Kind, Action Dispose, Func<ValueTask> DisposeAsync);
