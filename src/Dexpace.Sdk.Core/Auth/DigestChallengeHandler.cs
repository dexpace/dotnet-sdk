// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// Answers a <c>Digest</c> challenge (RFC 7616; AUTH-15 to AUTH-22, AUTH-24).
/// </summary>
/// <remarks>
/// <para>
/// <b>Algorithms.</b> <see cref="DigestAlgorithm.Md5"/>, <see cref="DigestAlgorithm.Md5Sess"/>,
/// <see cref="DigestAlgorithm.Sha256"/> and <see cref="DigestAlgorithm.Sha256Sess"/>, with <c>qop=auth</c> or the legacy
/// RFC 2069 form when a challenge sends no <c>qop</c>. The handler walks its <b>preference</b> list, not the order the
/// challenges arrived in; the default is SHA-256, SHA-256-sess, MD5, MD5-sess (P6c-29). A caller passes any non-empty,
/// duplicate-free subset. A host whose crypto provider refuses MD5 (FIPS) drops the MD5 algorithms, so an MD5-only
/// challenge is declined and a server offering both falls through to SHA-256; construction never fails there (P6c-30).
/// </para>
/// <para>
/// <b>Declined challenges.</b> A challenge is declined, never answered wrongly, when it lacks <c>realm</c> or <c>nonce</c>,
/// offers only <c>auth-int</c>, names an unsupported algorithm, asks for a <c>-sess</c> algorithm without <c>qop</c> (its
/// response could not be verified, P6c-31), carries a realm, nonce or opaque that cannot be echoed as outbound header text,
/// or needs a credential character its charset cannot represent (P6c-32). A declined attempt consumes no <c>nc</c>.
/// </para>
/// <para>
/// <b>Encodings.</b> The credential is hashed as UTF-8 under <c>charset=UTF-8</c> (any case), as ISO-8859-1 otherwise. A
/// username that is not printable ASCII is sent as <c>username*</c> (RFC 8187). The <c>digest-uri</c> is the escaped
/// request-target the transport writes. The <c>cnonce</c> is 128 bits from the operating system's CSPRNG (AUTH-20,
/// XCUT-21). <c>rspauth</c> in <c>Authentication-Info</c> is not verified.
/// </para>
/// <para>
/// The handler answers a challenge and remembers nothing but counts: a bounded map of 1024 nonces to atomic counters
/// (AUTH-19, AUTH-24). There is no preemptive Digest.
/// </para>
/// </remarks>
public sealed class DigestChallengeHandler : IChallengeHandler
{
    private const int NonceCapacity = 1024;

    private readonly DigestCredential _credential;
    private readonly DigestAlgorithm[] _preference;
    private readonly bool _md5Available;
    private readonly Func<string> _cnonceFactory;
    private readonly BoundedMap<string, NonceCounter> _nonces = new(NonceCapacity);

    /// <summary>Initializes a handler with the <see cref="DefaultPreference"/>.</summary>
    /// <param name="credential">The credential to answer with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is <see langword="null"/>.</exception>
    public DigestChallengeHandler(DigestCredential credential)
        : this(credential, DefaultPreference)
    {
    }

    /// <summary>Initializes a handler with an explicit algorithm preference.</summary>
    /// <param name="credential">The credential to answer with.</param>
    /// <param name="preference">The algorithms to prefer, most preferred first: non-empty, without duplicates.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="preference"/> is empty, repeats an algorithm or holds an undefined value.</exception>
    public DigestChallengeHandler(DigestCredential credential, IEnumerable<DigestAlgorithm> preference)
        : this(credential, preference, Md5Availability.IsAvailable, static () => RandomNumberGenerator.GetHexString(32, lowercase: true))
    {
    }

    internal DigestChallengeHandler(
        DigestCredential credential,
        IEnumerable<DigestAlgorithm> preference,
        bool md5Available,
        Func<string> cnonceFactory)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(preference);
        ArgumentNullException.ThrowIfNull(cnonceFactory);
        var configured = preference.ToArray();
        if (configured.Length == 0)
        {
            throw new ArgumentException("A preference needs at least one algorithm.", nameof(preference));
        }

        if (configured.Any(static a => !Enum.IsDefined(a)) || configured.Distinct().Count() != configured.Length)
        {
            throw new ArgumentException("A preference holds only defined algorithms, each once.", nameof(preference));
        }

        _credential = credential;
        _md5Available = md5Available;
        _cnonceFactory = cnonceFactory;

        // The effective preference: what was configured, less the MD5 algorithms a FIPS host refuses.
        _preference = [.. configured.Where(a => md5Available || !DigestAlgorithmNames.IsMd5(a))];
    }

    /// <summary>The default preference: SHA-256, SHA-256-sess, MD5, MD5-sess (strongest first, RFC 7616 section 3.7).</summary>
    public static IReadOnlyList<DigestAlgorithm> DefaultPreference { get; } =
        [DigestAlgorithm.Sha256, DigestAlgorithm.Sha256Sess, DigestAlgorithm.Md5, DigestAlgorithm.Md5Sess];

    internal int NonceStoreCapacity => _nonces.Capacity;

    internal bool Md5Available => _md5Available;

    /// <inheritdoc/>
    public Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy)
    {
        ArgumentNullException.ThrowIfNull(challenges);
        ArgumentNullException.ThrowIfNull(request);
        foreach (var candidate in Candidates(challenges))
        {
            if (TryRender(candidate, request) is { } value)
            {
                var header = proxy ? HttpHeaderName.WellKnown.ProxyAuthorization : HttpHeaderName.WellKnown.Authorization;
                return request.WithHeaders(request.Headers.Set(header.Original, value));
            }
        }

        return null;
    }

    internal void SetNextCount(string nonce, uint next) =>
        _nonces.GetOrAdd(nonce, static () => new NonceCounter()).SetNext(next);

    // AUTH-16: the satisfiable challenges, walked in the handler's preference order (wire order within one algorithm).
    private IEnumerable<Candidate> Candidates(IReadOnlyList<AuthenticationChallenge> challenges)
    {
        var satisfiable = new List<Candidate>();
        foreach (var challenge in challenges)
        {
            if (Satisfiable(challenge) is { } candidate)
            {
                satisfiable.Add(candidate);
            }
        }

        foreach (var algorithm in _preference)
        {
            foreach (var candidate in satisfiable)
            {
                if (candidate.Algorithm == algorithm)
                {
                    yield return candidate;
                }
            }
        }
    }

    private static Candidate? Satisfiable(AuthenticationChallenge challenge)
    {
        if (!string.Equals(challenge.Scheme, "digest", StringComparison.Ordinal))
        {
            return null;
        }

        var parameters = challenge.Parameters;
        if (!parameters.TryGetValue("realm", out var realm) || !parameters.TryGetValue("nonce", out var nonce))
        {
            return null;
        }

        parameters.TryGetValue("qop", out var qop);
        var qopAuth = qop is not null && HasAuth(qop);
        if (qop is not null && !qopAuth)
        {
            return null;
        }

        parameters.TryGetValue("algorithm", out var algorithmToken);
        if (!DigestAlgorithmNames.TryParse(algorithmToken, out var algorithm))
        {
            return null;
        }

        // P6c-31: a -sess response without qop could not be verified by the server (AUTH-16, AUTH-17 and AUTH-22 conflict).
        if (DigestAlgorithmNames.IsSession(algorithm) && !qopAuth)
        {
            return null;
        }

        parameters.TryGetValue("opaque", out var opaque);
        parameters.TryGetValue("charset", out var charset);
        return Echoable(realm) && Echoable(nonce) && (opaque is null || Echoable(opaque))
            ? new Candidate(algorithm, realm, nonce, qopAuth, opaque, string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase))
            : null;
    }

    // qop is a comma-separated token list compared token-exact: "auth-int" does not contain "auth".
    private static bool HasAuth(string qop)
    {
        foreach (var token in qop.Split(',', StringSplitOptions.TrimEntries))
        {
            if (string.Equals(token, "auth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // XCUT-18: an echoed value must be valid outbound header text, or the challenge is declined.
    private static bool Echoable(string value) => HeaderSyntax.IndexOfInvalidOutbound(value) < 0;

    // The credential is encoded first, the one step that can decline, and the nonce count taken after it, so a declined
    // attempt consumes none (P6c-35).
    private string? TryRender(Candidate candidate, Request request)
    {
        if (!DigestComputation.TryEncode(_credential.Username, candidate.Utf8, out var user)
            || !DigestComputation.TryEncode(_credential.Password, candidate.Utf8, out var password)
            || !DigestComputation.TryEncode(candidate.Realm, candidate.Utf8, out var realm))
        {
            return null;
        }

        var uri = DigestComputation.DigestUri(request.Url);
        var cnonce = _cnonceFactory();
        var qop = candidate.QopAuth ? "auth" : null;
        var nc = candidate.QopAuth ? DigestComputation.Nc(_nonces.GetOrAdd(candidate.Nonce, static () => new NonceCounter()).Next()) : string.Empty;
        var response = DigestComputation.Compute(
            candidate.Algorithm,
            user,
            realm,
            password,
            candidate.Nonce,
            request.Method.ToString(),
            uri,
            qop,
            nc,
            cnonce);
        return Render(candidate, user, uri, qop, nc, cnonce, response);
    }

    // AUTH-22: username, realm, nonce, uri, response, cnonce and opaque quoted; qop, nc and algorithm bare, the algorithm in
    // its full RFC spelling; cnonce, nc and qop only when qop was negotiated; opaque only when sent; never userhash.
    private string Render(Candidate candidate, byte[] user, string uri, string? qop, string nc, string cnonce, string response)
    {
        var builder = new StringBuilder("Digest ");
        builder.Append(UsernameField(user, candidate.Utf8));
        builder.Append(", realm=\"").Append(DigestComputation.Quote(candidate.Realm)).Append('"');
        builder.Append(", nonce=\"").Append(DigestComputation.Quote(candidate.Nonce)).Append('"');
        builder.Append(", uri=\"").Append(DigestComputation.Quote(uri)).Append('"');
        builder.Append(", algorithm=").Append(DigestAlgorithmNames.Wire(candidate.Algorithm));
        if (qop is not null)
        {
            builder.Append(", qop=").Append(qop).Append(", nc=").Append(nc);
            builder.Append(", cnonce=\"").Append(DigestComputation.Quote(cnonce)).Append('"');
        }

        builder.Append(", response=\"").Append(response).Append('"');
        if (candidate.Opaque is not null)
        {
            builder.Append(", opaque=\"").Append(DigestComputation.Quote(candidate.Opaque)).Append('"');
        }

        return builder.ToString();
    }

    // P6c-33: a username that is not printable ASCII goes out as RFC 8187's extended form (the outbound grammar refuses it).
    private string UsernameField(byte[] encodedUsername, bool utf8) =>
        DigestComputation.IsPrintableAscii(_credential.Username)
            ? "username=\"" + DigestComputation.Quote(_credential.Username) + "\""
            : "username*=" + DigestComputation.Rfc8187Encode(encodedUsername, utf8);

    private sealed record Candidate(DigestAlgorithm Algorithm, string Realm, string Nonce, bool QopAuth, string? Opaque, bool Utf8);
}
