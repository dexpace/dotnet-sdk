// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The composition the invariant suite judges (design section C, P10-4): a set of hooks, each defaulting to core's own public
/// component, so the suite is portable (a third party that composes its own policies, credentials or codec runs the same
/// assertions over its composition) and every assertion can have a negative control (a broken member in place of the default).
/// </summary>
/// <remarks>
/// <para>
/// <b>Four hooks have no default</b>, because the thing they reach is internal to core or lives in another package:
/// <see cref="MapStrict"/> (<c>XCUT-8</c>), <see cref="CreateCyclicException"/> (<c>XCUT-9</c>), <see cref="CreateBoundedMap"/>
/// (<c>XCUT-14</c>) and <see cref="SeamResolution"/> (<c>XCUT-23</c>). An assertion whose hook is <see langword="null"/> is
/// <see cref="ConformanceStatus.NotExercised"/> with a reason naming the hook, never passed: a driver that does not supply a hook
/// must pin the assertion in its expected-<c>NotExercised</c> list (P10-28).
/// </para>
/// <para>
/// <see cref="DrawCnonce"/> is optional and is not one of the four: <see langword="null"/> means the suite draws through the public
/// <c>DigestChallengeHandler.Authorize</c>, so <c>xcut-21</c> is always exercised. It judges the draw's width and that it does not
/// repeat; the operating-system CSPRNG source clause is cited by reference, because no output test can tell a CSPRNG from
/// <see cref="Random"/>.
/// </para>
/// </remarks>
public sealed record InvariantSubject
{
    private static readonly UrlRedactor s_redactor = new();

    /// <summary>The name the report carries. <c>core</c> by default.</summary>
    public string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = "core";

    /// <summary>
    /// Builds the pipeline under test over a transport, with the call-level options, the suite's recording time provider (every
    /// wait, timer and attempt deadline must run on it) and the suite's recording logger. Default: the idempotency, client-identity
    /// and set-date policies plus <see cref="PipelineBuilder.AddStandardResilience"/> over the transport, capturing the options.
    /// </summary>
    public Func<IAsyncHttpClient, DexpaceClientOptions, TimeProvider, ILogger, HttpPipeline> CreatePipeline
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = static (transport, options, time, logger) => new PipelineBuilder()
        .Add(new IdempotencyPolicy())
        .Add(new ClientIdentityPolicy())
        .Add(new SetDatePolicy(time))
        .AddStandardResilience(time, logger)
        .Build(transport, options);

    /// <summary>Builds the exception for a received error response (<c>XCUT-5</c>). Default: the public <see cref="HttpResponseException"/> constructor.</summary>
    public Func<Response, SdkException> CreateResponseException
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = static response => new HttpResponseException(response);

    /// <summary>Walks an exception's cause chain (<c>XCUT-9</c>). Default: <see cref="ExceptionFacts.EnumerateCauses"/>.</summary>
    public Func<Exception, IEnumerable<Exception>> EnumerateCauses
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = ExceptionFacts.EnumerateCauses;

    /// <summary>
    /// Builds a token cache over a credential and returns its read: given a request context, the token, fetching through the
    /// credential at most once per expiry window (<c>XCUT-12</c>). Default: <see cref="AccessTokenCache"/>.
    /// </summary>
    public Func<TokenCredential, TimeProvider, Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>>> CreateTokenCache
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = static (credential, time) =>
    {
        var cache = new AccessTokenCache(credential, time);
        return (context, token) => cache.GetAsync(context, token);
    };

    /// <summary>Redacts a URL for a log line or a span (<c>XCUT-19</c>(a)(b)). Default: <see cref="UrlRedactor.Redact(string)"/>.</summary>
    public Func<string, string> RedactUrl
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = static url => s_redactor.Redact(url);

    /// <summary>Redacts a URL-valued header value such as <c>Location</c> (<c>XCUT-19</c>). Default: <see cref="UrlRedactor.RedactHeaderValue"/>.</summary>
    public Func<string, string> RedactHeaderValue
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = static value => s_redactor.RedactHeaderValue(value);

    /// <summary>The model factories <c>XCUT-15</c> and <c>XCUT-18</c> judge. Default: core's public factories.</summary>
    public ModelFactories Models
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = new();

    /// <summary>The credential-stamping policies <c>XCUT-16</c> and <c>XCUT-17</c> drive. Default: <see cref="BasicAuthPolicy"/> and <see cref="BearerTokenAuthPolicy"/>.</summary>
    public AuthPolicyFactories Auth
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = new();

    /// <summary>The credential objects <c>XCUT-19</c>(d) renders. Default: core's Basic, API-key and Digest credentials and an access token.</summary>
    public IReadOnlyList<CredentialSample> Credentials
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = [.. value];
        }
    } = DefaultCredentials();

    /// <summary>
    /// Builds the disposable components <c>XCUT-13</c> closes, each over a fresh body: the caller closes one twice, and concurrently,
    /// and requires the body released exactly once, without waiting for a read parked on it. Default: a core <see cref="Response"/>,
    /// a <see cref="TypedResponse{T}"/> and a <see cref="ServerSentEventStream"/>.
    /// </summary>
    public Func<ResponseBody, IReadOnlyList<DisposableUnderTest>> CreateDisposables
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = DefaultDisposables;

    /// <summary>
    /// <b>No default.</b> The strict status-to-exception factory (<c>XCUT-8</c>): must throw <see cref="ArgumentException"/> for a
    /// non-error status. <see langword="null"/> makes <c>xcut-8.strict-factory-rejects</c> <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<Response, Exception>? MapStrict { get; init; }

    /// <summary>
    /// <b>No default.</b> Builds an exception whose cause chain is a cycle (<c>XCUT-9</c>). The suite only walks it and never throws
    /// it (a thrown cyclic chain hangs the test host on Windows). <see langword="null"/> makes <c>xcut-9.cycle-safe-walk</c>
    /// <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<Exception>? CreateCyclicException { get; init; }

    /// <summary>
    /// <b>No default.</b> Builds a bounded map of the given capacity (<c>XCUT-14</c>). <see langword="null"/> makes both
    /// <c>xcut-14</c> assertions <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public Func<int, BoundedMapProbe>? CreateBoundedMap { get; init; }

    /// <summary>
    /// <b>Optional.</b> One security-relevant random draw, such as a Digest <c>cnonce</c> (<c>XCUT-21</c>). <see langword="null"/> means
    /// the suite draws through the public <c>DigestChallengeHandler.Authorize</c>.
    /// </summary>
    public Func<string>? DrawCnonce { get; init; }

    /// <summary>
    /// <b>No default.</b> How the single-implementation seam resolves (<c>XCUT-23</c>). <see langword="null"/> makes
    /// <c>xcut-23.resolution-deterministic</c> <see cref="ConformanceStatus.NotExercised"/>.
    /// </summary>
    public SeamResolution? SeamResolution { get; init; }

#pragma warning disable CA2000 // Ownership passes to the returned disposables, which the assertion closes.
    private static DisposableUnderTest[] DefaultDisposables(ResponseBody body)
    {
        var request = Request.Get("https://example.test/");
        var headers = new Headers.Builder().Add("Content-Type", "text/event-stream").Build();
        var response = new Response(request, Status.Ok, Protocol.Http11, headers, body);
        var typed = new TypedResponse<int>(new Response(request, Status.Ok, Protocol.Http11, headers, body), new UnusedHandler());
        var events = ServerSentEventStream.FromResponse(new Response(request, Status.Ok, Protocol.Http11, headers, body));
        return
        [
            new DisposableUnderTest(nameof(Response), response.Dispose, response.DisposeAsync),
            new DisposableUnderTest(nameof(TypedResponse<int>), typed.Dispose, typed.DisposeAsync),
            new DisposableUnderTest(nameof(ServerSentEventStream), events.Dispose, events.DisposeAsync),
        ];
    }
#pragma warning restore CA2000

    private static CredentialSample[] DefaultCredentials()
    {
        var basic = new BasicCredential("service-user", "p4ssw0rd-secret");
        var apiKey = new ApiKeyCredential("api-key-secret-value");
        var digest = new DigestCredential("service-user", "d1gest-secret");
        var token = new AccessToken("access-token-secret");
        return
        [
            new CredentialSample(nameof(BasicCredential), "p4ssw0rd-secret", basic.ToString),
            new CredentialSample(nameof(ApiKeyCredential), "api-key-secret-value", apiKey.ToString),
            new CredentialSample(nameof(DigestCredential), "d1gest-secret", digest.ToString),
            new CredentialSample(nameof(AccessToken), "access-token-secret", token.ToString),
        ];
    }
}
