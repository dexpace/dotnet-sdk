// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>xcut-16</c> to <c>xcut-19</c> (plan 1.8).</summary>
[Trait("Category", "Unit")]
public sealed class InvariantSecurityControlTests
{
    internal static IReadOnlyList<InvariantControlRow> Rows { get; } =
    [
        .. Both("xcut-16.no-credential-over-http", "stamps its credential before it looks at the scheme", "must not be stamped over http", () => new InvariantSubject { Name = "broken: stamps over http", Auth = new AuthPolicyFactories { Basic = (_, _) => new BrokenPipelines.StampsWithoutGuard() } }),
        .. Both("xcut-16.no-credential-over-http", "fetches the token before the https guard", "token fetches", () => new InvariantSubject { Name = "broken: fetches first", Auth = new AuthPolicyFactories { Bearer = credential => new BrokenPipelines.FetchesBeforeGuard(credential) } }),
        .. Both("xcut-17.redirect-hygiene", "re-issues with the caller's Authorization", "even a same-origin one", () => Redirecting(BrokenPipelines.RedirectBug.KeepsAuthorization)),
        .. Both("xcut-17.redirect-hygiene", "keeps Cookie and Proxy-Authorization across origins", "cross-origin redirect", () => Redirecting(BrokenPipelines.RedirectBug.KeepsCookieAcrossOrigins)),
        .. Both("xcut-17.redirect-hygiene", "judges the origin against the previous hop", "judged against the seed", () => Redirecting(BrokenPipelines.RedirectBug.JudgesAgainstPreviousHop)),
        .. Both("xcut-17.redirect-hygiene", "re-stamps its credential on a foreign origin", "no credential re-stamped", () => Restamping(BrokenPipelines.RestampBug.AcrossOrigins)),
        .. Both("xcut-17.redirect-hygiene", "re-stamps a bearer credential on a foreign origin", "no credential re-stamped", () => RestampingBearer(BrokenPipelines.RestampBug.AcrossOrigins)),
        .. Both("xcut-17.redirect-hygiene", "stamps only the seed's own request", "re-stamped by the auth step", () => Restamping(BrokenPipelines.RestampBug.OnlyTheSeedRequest)),
        .. Both("xcut-17.redirect-hygiene", "re-attaches its credential when a foreign hop is retried", "retried", () => Restamping(BrokenPipelines.RestampBug.OnRetryAcrossOrigins)),
        new("xcut-18.model-validation", TransportFace.None, "accepts HTAB in a header name", () => Modelling(new ModelFactories { OutboundHeader = (name, value) => new Headers.Builder().Add(name.Contains('\t', StringComparison.Ordinal) ? "X-Tab" : name, value).Build() }), Because: "outbound header name"),
        new("xcut-18.model-validation", TransportFace.None, "drops a CR from an outbound value and accepts it", () => Modelling(new ModelFactories { OutboundHeader = (name, value) => new Headers.Builder().Add(name, value.Replace("\r", string.Empty, StringComparison.Ordinal)).Build() }), Because: "outbound header value"),
        new("xcut-18.model-validation", TransportFace.None, "accepts a CR or LF in an inbound value", () => Modelling(new ModelFactories { InboundHeader = (name, value) => new Headers.Builder().AddInbound(name, value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal)).Build() }), Because: "inbound header value"),
        new("xcut-18.model-validation", TransportFace.None, "stores an outbound name with HTAB at one end under another name", () => Modelling(new ModelFactories { OutboundHeader = (name, value) => new Headers.Builder().Add(EdgedByTab(name) ? "X-Edge" : name, value).Build() }), Because: "stored name of an outbound"),
        new("xcut-18.model-validation", TransportFace.None, "stores an inbound name with HTAB at one end under another name", () => Modelling(new ModelFactories { InboundHeader = (name, value) => new Headers.Builder().AddInbound(EdgedByTab(name) ? "X-Edge" : name, value).Build() }), Because: "stored name of an inbound"),
        .. Both("xcut-19.redaction-default-deny", "redacts nothing", "redacted URL", () => new InvariantSubject { Name = "broken: allow-all redactor", RedactUrl = url => url }),
        .. Both("xcut-19.redaction-default-deny", "renders a credential with its secret", "must not reveal its secret", () => new InvariantSubject { Name = "broken: leaky credential", Credentials = [new CredentialSample("Leaky", "leaky-secret-123", () => "Leaky { Secret = leaky-secret-123 }")] }),
        .. Both("xcut-19.redaction-default-deny", "logs request headers verbatim", "logging must redact", () => Logging(logger => new BrokenPipelines.LogsHeadersVerbatim(logger))),
        .. Both("xcut-19.redaction-default-deny", "logs the response body", "logging must redact", () => Logging(logger => new BrokenPipelines.LogsBody(logger))),
    ];

    public static IReadOnlyCollection<string> Covered => InvariantControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => InvariantControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => InvariantControlRow.PositiveCases(Rows);

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => InvariantControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_default_subject_passes(string assertion, TransportFace face) => InvariantControlRow.RunPositiveAsync(assertion, face);

    // XCUT-18 refuses a name with HTAB and HTTP-17 trims surrounding whitespace: a model may do either at the edge of a name (core
    // trims; this one refuses), and both pass. Only a model that keeps the name under another form is caught.
    [Fact]
    public Task A_model_that_refuses_a_name_with_HTAB_at_either_end_passes() =>
        InvariantControlRow.RunPositiveAsync(
            "xcut-18.model-validation",
            TransportFace.None,
            Modelling(new ModelFactories
            {
                OutboundHeader = (name, value) => EdgedByTab(name) ? throw new ArgumentException("a header name must be a token") : new Headers.Builder().Add(name, value).Build(),
                InboundHeader = (name, value) => EdgedByTab(name) ? throw new ArgumentException("a header name must be a token") : new Headers.Builder().AddInbound(name, value).Build(),
            }));

    private static bool EdgedByTab(string name) => name.Length > 0 && (name[0] == '\t' || name[^1] == '\t');

    private static InvariantSubject Redirecting(BrokenPipelines.RedirectBug bug) =>
        BrokenPipelines.Subject("redirect: " + bug, _ => [new BrokenPipelines.BrokenRedirect(bug)]);

    private static InvariantSubject Restamping(BrokenPipelines.RestampBug bug) =>
        new() { Name = "broken: restamp " + bug, Auth = new AuthPolicyFactories { Basic = (_, _) => new BrokenPipelines.BrokenRestamp(bug, "Basic c3ZjOnNlY3JldA==") } };

    private static InvariantSubject RestampingBearer(BrokenPipelines.RestampBug bug) =>
        new() { Name = "broken: restamp bearer " + bug, Auth = new AuthPolicyFactories { Bearer = _ => new BrokenPipelines.BrokenRestamp(bug, "Bearer broken-token") } };

    private static InvariantSubject Modelling(ModelFactories models) => new() { Name = "broken: models", Models = models };

    private static InvariantSubject Logging(Func<Microsoft.Extensions.Logging.ILogger, HttpPipelinePolicy> extra) =>
        new()
        {
            Name = "broken: logging",
            CreatePipeline = (transport, options, time, logger) =>
                PipelineBuilder.Flatten(new InvariantSubject().CreatePipeline(transport, options, time, logger)).Add(extra(logger)).Build(),
        };

    private static InvariantControlRow[] Both(string assertion, string name, string because, Func<InvariantSubject> subject) =>
        [
            new(assertion, TransportFace.Async, name, subject, Because: because),
            new(assertion, TransportFace.Blocking, name, subject, Because: because),
        ];
}
