// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The base type for every exception the SDK raises.
/// </summary>
/// <remarks>
/// The hierarchy distinguishes three transport failure shapes —
/// <see cref="ServiceRequestException"/> (request never reached the server, safe to retry on
/// idempotent methods), <see cref="ServiceResponseException"/> (request sent but the response
/// could not be read), and <see cref="HttpResponseException"/> (a 4xx/5xx received intact) —
/// alongside body/stream lifecycle, serialization, and pipeline failures.
/// </remarks>
public class SdkException : Exception, IRetryableError
{
    private const int MaxTrailRenderDepth = 8;

    [ThreadStatic]
    private static HashSet<Exception>? s_rendering;

    [ThreadStatic]
    private static int s_renderDepth;

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
    public override string ToString()
    {
        var text = base.ToString();
        var trail = Volatile.Read(ref _trail)?.Snapshot();
        if (trail is null || trail.Count == 0 || s_renderDepth >= MaxTrailRenderDepth)
        {
            return text;
        }

        var rendering = s_rendering ??= new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        if (!rendering.Add(this))
        {
            return text;
        }

        s_renderDepth++;
        try
        {
            var builder = new StringBuilder(text);
            for (var i = 0; i < trail.Count; i++)
            {
                var inner = trail[i];
                builder.Append(Environment.NewLine).Append("---> (Suppressed Exception #").Append(i).Append(") ");
                builder.Append(rendering.Contains(inner) ? "(cycle)" : inner.ToString());
                builder.Append("<---");
            }

            return builder.ToString();
        }
        finally
        {
            s_renderDepth--;
            rendering.Remove(this);
        }
    }
}
