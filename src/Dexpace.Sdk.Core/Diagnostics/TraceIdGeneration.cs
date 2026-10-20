// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// Trace-id generators an application can opt into: a W3C flavour and a Datadog flavour, neither ever the reserved all-zero id
/// (OBS-27, design §10 entry 24, P11-18).
/// </summary>
/// <remarks>
/// <para>
/// <b>The SDK never installs one.</b> The process-wide hook <c>Activity.TraceIdGenerator</c> belongs to the application, and a
/// library that set it would take it from every other library in the process (the hook is on the banned-API list for that reason).
/// The application opts in with one line at startup: <c>Activity.TraceIdGenerator = TraceIdGeneration.Datadog;</c> (or
/// <c>TraceIdGeneration.W3C</c>, which draws what the runtime would draw, with the zero draw ruled out). The no-op flavour needs no
/// generator: it is the invalid sentinel, <c>default(ActivityTraceId)</c>, which <c>InstrumentationContext.None</c> carries.
/// </para>
/// <para>
/// <b>Zero draws are drawn again.</b> The runtime's own generator (<see cref="ActivityTraceId.CreateRandom"/>) draws two 64-bit
/// values and tests neither for zero, so a zero draw would yield the reserved id. Both generators here take their bytes from
/// <see cref="RandomNumberGenerator"/> and draw again until the bytes that carry the id are not all zero, which is the non-zero
/// guarantee of OBS-27 by re-draw rather than by coercion.
/// </para>
/// <para>
/// <b>The Datadog flavour</b> is a 64-bit unsigned trace id. It is carried in an <see cref="ActivityTraceId"/> as the low 64 bits of
/// the 128-bit W3C id with a zero high half, so it propagates as a valid <c>traceparent</c>; <see cref="FormatDatadog"/> renders
/// the decimal string Datadog's own headers use.
/// </para>
/// </remarks>
public static class TraceIdGeneration
{
    private const int TraceIdBytes = 16;
    private const int HalfBytes = TraceIdBytes / 2;

    /// <summary>
    /// Generates a W3C trace id: 128 random bits, rendered by <see cref="ActivityTraceId.ToHexString"/> as 32 lowercase hex
    /// characters, never all-zero (OBS-27).
    /// </summary>
    /// <remarks>Assign the method group to <c>Activity.TraceIdGenerator</c> to use it: <c>Activity.TraceIdGenerator = TraceIdGeneration.W3C;</c>.</remarks>
    /// <returns>A non-zero trace id.</returns>
    public static ActivityTraceId W3C() => W3C(RandomNumberGenerator.Fill);

    /// <summary>
    /// Generates a Datadog trace id: a non-zero 64-bit unsigned value in the low half of the id, with a zero high half, so that
    /// <see cref="FormatDatadog"/> renders it as a decimal string in range (OBS-27).
    /// </summary>
    /// <remarks>Assign the method group to <c>Activity.TraceIdGenerator</c> to use it: <c>Activity.TraceIdGenerator = TraceIdGeneration.Datadog;</c>.</remarks>
    /// <returns>A trace id whose high 64 bits are zero and whose low 64 bits are not.</returns>
    public static ActivityTraceId Datadog() => Datadog(RandomNumberGenerator.Fill);

    /// <summary>Renders the Datadog trace id held in the low 64 bits of <paramref name="traceId"/> as an unsigned decimal string (OBS-27).</summary>
    /// <remarks>
    /// An id from <see cref="Datadog()"/> has a zero high half, so nothing is dropped. For any other id only the low 64 bits are
    /// rendered, because a Datadog trace id has no more than 64.
    /// </remarks>
    /// <param name="traceId">A trace id whose low 64 bits are not all zero.</param>
    /// <returns>The low 64 bits as a decimal string with no sign and no leading zeros.</returns>
    /// <exception cref="ArgumentException"><paramref name="traceId"/> is the invalid sentinel, or its low 64 bits are zero (a Datadog trace id of 0 is reserved).</exception>
    public static string FormatDatadog(ActivityTraceId traceId)
    {
        if (traceId == default)
        {
            throw new ArgumentException("The default trace id is the invalid sentinel and has no Datadog form.", nameof(traceId));
        }

        var value = ulong.Parse(traceId.ToHexString().AsSpan(HalfBytes * 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (value == 0)
        {
            throw new ArgumentException("A Datadog trace id of 0 is reserved; the low 64 bits of the id are zero.", nameof(traceId));
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    // The byte source is a parameter so a test can hand the generators a zero draw; production passes the CSPRNG.
    internal static ActivityTraceId W3C(Action<Span<byte>> fill)
    {
        Span<byte> bytes = stackalloc byte[TraceIdBytes];
        do
        {
            fill(bytes);
        }
        while (IsZero(bytes));

        return ActivityTraceId.CreateFromBytes(bytes);
    }

    internal static ActivityTraceId Datadog(Action<Span<byte>> fill)
    {
        Span<byte> bytes = stackalloc byte[TraceIdBytes];
        bytes.Clear();
        var low = bytes[HalfBytes..];
        do
        {
            fill(low);
        }
        while (IsZero(low));

        return ActivityTraceId.CreateFromBytes(bytes);
    }

    private static bool IsZero(ReadOnlySpan<byte> bytes) => bytes.IndexOfAnyExcept((byte)0) < 0;
}
