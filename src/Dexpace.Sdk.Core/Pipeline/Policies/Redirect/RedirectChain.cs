// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The per-call state of one redirect run (REDIR-8, REDIR-16): the seed origin, the original method, the current request,
/// the visited keys and the followed count. Lives on the stack of one <c>ProcessCoreAsync</c>; never shared.
/// </summary>
internal sealed class RedirectChain
{
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly List<Uri> _visited = [];

    internal RedirectChain(Request first, PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(context);

        // REDIR-8 / REDIR-11: cross-origin is judged against the seed on the call-scoped context, never a previous hop.
        SeedOrigin = HttpOrigin.From(context.SeedRequest.Url);

        // REDIR-3 (P6b-4): "original" is the method of the first request this policy drives.
        OriginalMethod = first.Method;
        Current = first;
        Record(WithoutUserInfo(first.Url));
    }

    /// <summary>The origin of the call's seed request.</summary>
    internal HttpOrigin SeedOrigin { get; }

    /// <summary>The method of the first request the policy drove.</summary>
    internal Method OriginalMethod { get; }

    /// <summary>The request of the hop being judged.</summary>
    internal Request Current { get; private set; }

    /// <summary>Hops followed so far.</summary>
    internal int Followed { get; private set; }

    /// <summary>
    /// The visited key of a URI: its userinfo-free <c>AbsoluteUri</c>, compared ordinally (HTTP-46). Never
    /// <c>Uri.Equals</c> (it ignores fragment and userinfo) and never <c>ToString</c> (lossy).
    /// </summary>
    internal static string KeyOf(Uri uri) => WithoutUserInfo(uri).AbsoluteUri;

    /// <summary>Whether <paramref name="key"/> was already visited on this call.</summary>
    internal bool Contains(string key) => _keys.Contains(key);

    /// <summary>Moves to the next hop: records the target and counts the hop.</summary>
    internal void Advance(Request next, Uri target)
    {
        Record(WithoutUserInfo(target));
        Current = next;
        Followed++;
    }

    /// <summary>Takes the immutable snapshot a predicate sees (REDIR-20).</summary>
    internal RedirectCondition Snapshot(Response response, Uri? target) =>
        new(response, Followed, [.. _visited], target);

    private static Uri WithoutUserInfo(Uri uri) =>
        uri.UserInfo.Length == 0
            ? uri
            : new Uri(uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped));

    private void Record(Uri uri)
    {
        if (_keys.Add(uri.AbsoluteUri))
        {
            _visited.Add(uri);
        }
    }
}
