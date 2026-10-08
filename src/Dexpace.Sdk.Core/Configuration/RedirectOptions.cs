// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Pipeline.Policies;

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// Options for the redirect-following policy (REDIR-3 to REDIR-5, REDIR-15, REDIR-17, REDIR-20, REDIR-26).
/// </summary>
/// <remarks>
/// <para>
/// A sealed record with <see langword="init"/> accessors (CFG-8): derive a modified copy with <see langword="with"/>
/// (CFG-9). Equality is by value: <see cref="AllowedMethods"/> compares by content and <see cref="Predicate"/> by delegate
/// equality.
/// </para>
/// <para>
/// <b>Breaking:</b> this was a mutable class; assigning a property after construction no longer compiles, and equality
/// and the hash code are by value (was: by reference).
/// </para>
/// <para>
/// <b>Breaking:</b> <c>StripSensitiveHeadersOnCrossOrigin</c> is removed. It has had no effect since phase 1:
/// <c>Authorization</c> is removed on every hop and <c>Cookie</c> and <c>Proxy-Authorization</c> on a cross-origin hop
/// whatever the options say, because those are requirements and a switch must not narrow them.
/// </para>
/// </remarks>
public sealed record RedirectOptions
{
    private static readonly FrozenSet<Method> s_defaultMethods = new[] { Method.Get, Method.Head }.ToFrozenSet();

    /// <summary>
    /// The maximum number of redirect hops to follow. Defaults to <c>3</c>; <c>0</c> follows nothing and returns the
    /// first 3xx verbatim (REDIR-17, REDIR-25).
    /// </summary>
    /// <remarks><b>Breaking:</b> the default was <c>20</c>, and a negative value is now rejected.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxRedirects
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 3;

    /// <summary>
    /// When <see langword="true"/>, the policy follows <c>https → http</c> downgrade redirects, and emits the
    /// <c>http.redirect.scheme_downgrade_permitted</c> warning when it does. Defaults to <see langword="false"/>: a downgrade
    /// throws <see cref="Errors.RedirectSchemeDowngradeException"/> (REDIR-15).
    /// </summary>
    /// <remarks><b>Breaking:</b> without the opt-in the 3xx used to be returned; it now throws.</remarks>
    public bool AllowHttpsToHttpDowngrade { get; init; }

    /// <summary>
    /// When <see langword="true"/>, a <c>303 See Other</c> is followed as a body-less <c>GET</c> (REDIR-5). Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks><b>Breaking:</b> a 303 used to be followed by default.</remarks>
    public bool FollowSeeOther { get; init; }

    /// <summary>
    /// The methods whose <c>301</c>, <c>302</c>, <c>307</c> and <c>308</c> redirects are followed, with the method and the body
    /// preserved (REDIR-3, REDIR-4). Defaults to <c>{GET, HEAD}</c>. The set is copied when assigned (REDIR-26), so a later
    /// change to the caller's collection has no effect. An empty set is allowed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eligibility is judged on the <em>original</em> method, the one the call began with, never on the method of a later
    /// hop (P6b-4): a 301 that follows an opted-in 303 is judged as the original <c>POST</c> was.
    /// </para>
    /// <para>
    /// <b>Breaking:</b> a 301 or 302 on a <c>POST</c> used to be rewritten to a body-less <c>GET</c>; it is now followed
    /// only when <c>POST</c> is in this set, and then as a <c>POST</c> with its body. Otherwise the 3xx is returned.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value, or an element, is <see langword="null"/>.</exception>
    public IReadOnlySet<Method> AllowedMethods
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var method in value)
            {
                ArgumentNullException.ThrowIfNull(method, nameof(value));
            }

            field = value.ToFrozenSet();
        }
    } = s_defaultMethods;

    /// <summary>
    /// A predicate that replaces the built-in eligibility decision (the allowed-method set and <see cref="FollowSeeOther"/>)
    /// for a recognised 3xx (REDIR-20, REDIR-21). It cannot defeat loop detection, the hop cap, the <c>Location</c> screen, the
    /// downgrade guard, the replayability gate or credential stripping. Defaults to <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// An exception it throws propagates unchanged, after the response is disposed. It must not dispose the response or
    /// read its body.
    /// </remarks>
    public Func<RedirectCondition, bool>? Predicate { get; init; }

    /// <summary>Equality over every member; <see cref="AllowedMethods"/> by content, <see cref="Predicate"/> by delegate equality.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(RedirectOptions? other) =>
        other is not null
        && MaxRedirects == other.MaxRedirects
        && AllowHttpsToHttpDowngrade == other.AllowHttpsToHttpDowngrade
        && FollowSeeOther == other.FollowSeeOther
        && Equals(Predicate, other.Predicate)
        && AllowedMethods.SetEquals(other.AllowedMethods);

    /// <summary>Returns a hash code consistent with <see cref="Equals(RedirectOptions?)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MaxRedirects);
        hash.Add(AllowHttpsToHttpDowngrade);
        hash.Add(FollowSeeOther);
        hash.Add(Predicate);
        hash.Add(AllowedMethods.Count);

        // An order-independent combination, so two sets with equal content hash alike whatever their insertion order.
        var methods = 0;
        foreach (var method in AllowedMethods)
        {
            methods ^= method.GetHashCode();
        }

        hash.Add(methods);
        return hash.ToHashCode();
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("MaxRedirects = ").Append(MaxRedirects);
        builder.Append(", AllowHttpsToHttpDowngrade = ").Append(AllowHttpsToHttpDowngrade);
        builder.Append(", FollowSeeOther = ").Append(FollowSeeOther);
        builder.Append(", AllowedMethods = [").AppendJoin(", ", AllowedMethods.Select(static m => m.Name).Order(StringComparer.Ordinal)).Append(']');
        builder.Append(", Predicate = ").Append(Predicate is null ? "(none)" : "set");
        return true;
    }
}
