// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The one rendering of an exception's suppressed trail, shared by <see cref="SdkException.ToString"/> and
/// <see cref="ServiceRequestException.ToString"/> (RETRY-34, TRANSPORT-20; design §5.2).
/// </summary>
/// <remarks>
/// A trail that leads back to an exception being rendered shows <c>(cycle)</c>, and nesting is capped at 8 levels. The guard state is per thread and
/// shared by both exception families, so a cycle that crosses from a transport failure to an <see cref="SdkException"/> and back still terminates.
/// </remarks>
internal static class SuppressedTrailRenderer
{
    private const int MaxDepth = 8;

    [ThreadStatic]
    private static HashSet<Exception>? s_rendering;

    [ThreadStatic]
    private static int s_depth;

    /// <summary>Appends one <c>---&gt; (Suppressed Exception #n) ...&lt;---</c> block per suppressed exception to <paramref name="text"/>.</summary>
    /// <param name="exception">The exception being rendered.</param>
    /// <param name="text">Its base rendering (<see cref="Exception.ToString"/>).</param>
    /// <returns><paramref name="text"/> unchanged when there is no trail, a cycle is being rendered, or the nesting cap is reached.</returns>
    internal static string Render(Exception exception, string text)
    {
        var trail = ExceptionTrail.GetSuppressed(exception);
        if (trail.Count == 0 || s_depth >= MaxDepth)
        {
            return text;
        }

        var rendering = s_rendering ??= new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        if (!rendering.Add(exception))
        {
            return text;
        }

        s_depth++;
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
            s_depth--;
            rendering.Remove(exception);
        }
    }
}
