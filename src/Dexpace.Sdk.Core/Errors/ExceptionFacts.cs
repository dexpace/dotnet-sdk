// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// Shared exception predicates every SDK catch site and recovery step uses (design §5.2, §10 entry 12; XCUT-9, RETRY-25).
/// </summary>
public static class ExceptionFacts
{
    private const int MaxCauseDepth = 64;

    /// <summary>
    /// Whether <paramref name="exception"/> is fatal: an <see cref="OutOfMemoryException"/> or one of its subtypes.
    /// </summary>
    /// <remarks>
    /// Tests the exception itself, not its chain: <c>await</c> unwraps, so a fatal exception reaches a catch unwrapped,
    /// and one a caller wrapped in a non-fatal exception is the caller's decision. Every broad catch in the SDK reads
    /// <c>catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))</c>.
    /// </remarks>
    /// <param name="exception">The exception to test.</param>
    /// <returns><see langword="true"/> when the exception must never be converted, logged or suppressed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static bool IsFatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is OutOfMemoryException;
    }

    /// <summary>
    /// Enumerates <paramref name="exception"/> <b>root first</b>, then its causes breadth-first over
    /// <see cref="Exception.InnerException"/> and <see cref="AggregateException.InnerExceptions"/>.
    /// </summary>
    /// <remarks>
    /// The name reads the other way, but every consumer is a classification that must test the head too. Nodes are
    /// tracked by reference identity (so a type overriding <c>Equals</c> cannot hide a distinct cause), a cycle
    /// terminates, an aggregate's first inner exception is yielded once, and the walk stops below depth 64.
    /// </remarks>
    /// <param name="exception">The root exception.</param>
    /// <returns>The exception followed by its causes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Exception> EnumerateCauses(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Walk(exception);
    }

    private static IEnumerable<Exception> Walk(Exception root)
    {
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance) { root };
        var queue = new Queue<(Exception Node, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            yield return node;
            if (depth >= MaxCauseDepth)
            {
                continue;
            }

            if (node.InnerException is { } inner && visited.Add(inner))
            {
                queue.Enqueue((inner, depth + 1));
            }

            if (node is AggregateException aggregate)
            {
                foreach (var child in aggregate.InnerExceptions)
                {
                    if (child is not null && visited.Add(child))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }
        }
    }
}
