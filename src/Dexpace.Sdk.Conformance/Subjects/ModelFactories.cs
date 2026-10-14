// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The wire models the invariant suite builds, as delegates (P10-4): <c>XCUT-15</c> (no model aliases a mutable input) and
/// <c>XCUT-18</c> (names and values are validated before a transport sees them). Each member takes the mutable input and
/// returns the model, or the projection of the model the assertion inspects. The defaults call core's public factories; a
/// negative control replaces one with a model that breaks the rule.
/// </summary>
public sealed record ModelFactories
{
    /// <summary>
    /// Builds a header collection holding one field through the outbound path (name, value) and returns it; throws
    /// <see cref="ArgumentException"/> for a name or value the outbound grammar refuses. Default: <c>Headers.Builder.Add</c>.
    /// </summary>
    public Func<string, string, Headers> OutboundHeader { get; init; } = static (name, value) => new Headers.Builder().Add(name, value).Build();

    /// <summary>
    /// Builds a header collection holding one field through the inbound (lenient) path; throws <see cref="ArgumentException"/> for a
    /// control byte other than HTAB. Default: <c>Headers.Builder.AddInbound</c>.
    /// </summary>
    public Func<string, string, Headers> InboundHeader { get; init; } = static (name, value) => new Headers.Builder().AddInbound(name, value).Build();

    /// <summary>
    /// Builds a media type from a mutable parameter map and returns the parameters of the model; the suite mutates the map
    /// afterwards and requires the model unchanged. Default: <c>MediaType.Of(...).Parameters</c>.
    /// </summary>
    public Func<Dictionary<string, string>, IReadOnlyDictionary<string, string>> MediaTypeParameters { get; init; } =
        static input => MediaType.Of("text", "plain", input).Parameters;

    /// <summary>
    /// Builds an authentication challenge from a mutable parameter map and returns the parameters of the model; the suite mutates
    /// the map afterwards and requires the model unchanged. Default: <c>new AuthenticationChallenge(...).Parameters</c>.
    /// </summary>
    public Func<Dictionary<string, string>, IReadOnlyDictionary<string, string>> ChallengeParameters { get; init; } =
        static input => new AuthenticationChallenge("Bearer", input).Parameters;

    /// <summary>
    /// Builds a request body from a mutable byte array and returns a reader of what the body would now send; the suite mutates
    /// the array between the two calls and requires the body unchanged. Default: <c>RequestBody.FromBytes</c>.
    /// </summary>
    public Func<byte[], Func<byte[]>> BytesBody { get; init; } = static input =>
    {
        var body = RequestBody.FromBytes(input);
        return () =>
        {
            using var sink = new MemoryStream();
            body.WriteTo(sink);
            return sink.ToArray();
        };
    };

    /// <summary>Returns the given collection with one field set, as a collection that is not the given one. Default: <c>Headers.With</c>.</summary>
    public Func<Headers, string, string, Headers> WithHeader { get; init; } = static (headers, name, value) => headers.With(name, value);

    /// <summary>Returns the given request with one field set, as a request that is not the given one. Default: <c>Request.WithHeader</c>.</summary>
    public Func<Request, string, string, Request> RequestWithHeader { get; init; } = static (request, name, value) => request.WithHeader(name, value);
}
