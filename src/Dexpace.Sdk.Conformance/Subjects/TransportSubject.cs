// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The transport under test, as a descriptor of factories (design "The subject is a factory descriptor", P8a-9; Ruby
/// R7: a factory, not a mixin). The suite calls a factory afresh for every assertion, so no assertion sees another's
/// state, and it disposes every transport it builds, on every path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Faces.</b> <see cref="CreateAsync"/> and <see cref="CreateBlocking"/> supply the two faces; either may be absent. An
/// assertion runs once per face it declares and the subject supplies; a face the subject lacks yields
/// <see cref="ConformanceStatus.NotExercised"/> for that pair.
/// </para>
/// <para>
/// <b>Capability hooks.</b> Each optional hook reaches a clause only a transport-specific construction can reach. A missing
/// hook makes the assertion that needs it report <see cref="ConformanceStatus.NotExercised"/> naming the hook, never
/// <see cref="ConformanceStatus.Passed"/> and never <see cref="ConformanceStatus.Vacuous"/>: "this transport has no such
/// construction" is not "the clause's antecedent is absent".
/// </para>
/// <para>
/// <b>What the suite never assumes</b> (suite contract): the transport's identity, a response header's exact spelling, a
/// <c>Content-Length</c> among a response's headers (the length is asked of the body), or anything beyond plaintext
/// HTTP/1.1. No assertion branches on a type name; a clause that differs per transport is a hook or a waiver.
/// </para>
/// </remarks>
public sealed class TransportSubject
{
    /// <summary>The name the report uses for the subject. Not empty.</summary>
    public required string Name
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A subject needs a name.", nameof(value)) : value;
        }
    }

    /// <summary>
    /// Builds the transport for the asynchronous face (<c>IAsyncHttpClient</c>), owned by the suite. Absent: every
    /// assertion on <see cref="TransportFace.Async"/> is <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<TransportSettings, IAsyncHttpClient>? CreateAsync { get; init; }

    /// <summary>
    /// Builds the transport for the blocking face (<c>IHttpClient</c>), owned by the suite. Absent: every assertion on
    /// <see cref="TransportFace.Blocking"/> is <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<TransportSettings, IHttpClient>? CreateBlocking { get; init; }

    /// <summary>
    /// Hook for <c>TRANSPORT-15</c> and <c>XCUT-22</c>: a transport over a native client the caller owns. Absent: the
    /// borrowed half of <c>transport-15</c> is <see cref="ConformanceStatus.NotExercised"/> (a transport with no native
    /// client to borrow).
    /// </summary>
    public Func<TransportSettings, BorrowedTransport>? CreateBorrowed { get; init; }

    /// <summary>
    /// Hook for <c>TRANSPORT-22</c>: a transport whose adaptation of every live native response fails after the response
    /// exists, so the suite can observe that the failed adaptation still releases the exchange. Absent:
    /// <c>transport-22</c> is <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<TransportSettings, IAsyncHttpClient>? CreateWithFaultingAdaptation { get; init; }

    /// <summary>
    /// Hook for <c>TRANSPORT-8</c>: a transport plus a trigger that cancels its in-flight calls from the native side.
    /// Absent: <c>transport-8</c> is <see cref="ConformanceStatus.NotExercised"/> (a transport with no such native
    /// cancellation, which Node and Ruby scope to their reference transports).
    /// </summary>
    public Func<TransportSettings, InternalCancellation>? CreateWithInternalCancel { get; init; }

    /// <summary>
    /// Hook for <c>TRANSPORT-18</c>: a transport whose native layer re-sends a request, as an HTTP client does after an
    /// authentication challenge or a stale connection, so the suite can observe what a single-use body does the second
    /// time. Absent: <c>transport-18</c> is <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<TransportSettings, IAsyncHttpClient>? CreateWithNativeResend { get; init; }

    /// <summary>
    /// Hook for <c>TRANSPORT-30</c>: a transport that installs the given <see cref="ProxyOptions"/>. Absent:
    /// <c>transport-30</c> is <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<TransportSettings, ProxyOptions, IAsyncHttpClient>? CreateWithProxy { get; init; }

    /// <summary>
    /// What a call made after the transport was disposed does (<c>SEAM-15</c>, a MAY). A declaration, not a hook: with
    /// <see cref="AfterDisposeBehavior.Unspecified"/> the <c>seam-15</c> assertion is <see cref="ConformanceStatus.Vacuous"/>.
    /// </summary>
    public AfterDisposeBehavior AfterDispose { get; init; }
}
