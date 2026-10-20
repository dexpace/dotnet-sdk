// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The base type for every exception the SDK raises.
/// </summary>
/// <remarks>
/// <para>
/// The hierarchy has two branches (XCUT-4): a failure with a response, <see cref="HttpResponseException"/> (a 4xx/5xx received intact), which is an
/// <see cref="SdkException"/>, and a transport failure with none, <see cref="ServiceRequestException"/>, which is an <see cref="IOException"/> and not an
/// <see cref="SdkException"/> (TRANSPORT-20 requires the platform I/O type; design §10 entry 7, phase 11). Both report <see cref="IRetryableError"/>.
/// Body/stream lifecycle, serialization and pipeline failures are <see cref="SdkException"/>s too.
/// </para>
/// <para>
/// <b>Breaking (phase 11):</b> <c>catch (SdkException)</c> no longer catches a transport failure; catch <see cref="ServiceRequestException"/> or
/// <see cref="IOException"/>.
/// </para>
/// </remarks>
public class SdkException : Exception, IRetryableError
{
    private SuppressedTrail? _trail;

    /// <summary>Initializes a new instance.</summary>
    public SdkException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public SdkException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public SdkException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets a value indicating whether retrying the failed call may succeed (XCUT-6). <see langword="false"/> unless a
    /// subtype overrides it.
    /// </summary>
    public virtual bool IsRetryable => false;

    /// <summary>
    /// The exceptions suppressed under this one, as an immutable snapshot (RECOV-12, RETRY-34); empty when none.
    /// </summary>
    public IReadOnlyList<Exception> Suppressed => _trail?.Snapshot() ?? [];

    internal SuppressedTrail Trail
    {
        get
        {
            var trail = Volatile.Read(ref _trail);
            if (trail is not null)
            {
                return trail;
            }

            Interlocked.CompareExchange(ref _trail, new SuppressedTrail(), null);
            return _trail!;
        }
    }

    /// <summary>Renders the exception, followed by one block per suppressed exception.</summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking:</b> the rendering was the base <see cref="Exception"/> text only; it now appends
    /// <c>---&gt; (Suppressed Exception #n) ...&lt;---</c> for each suppressed exception. A trail that leads back to an
    /// exception being rendered shows <c>(cycle)</c>, and nesting is capped at 8 levels.
    /// </para>
    /// </remarks>
    /// <returns>The text.</returns>
    public override string ToString() => SuppressedTrailRenderer.Render(this, base.ToString());
}
