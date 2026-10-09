// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SSE-38 (design §7.2, P7b-18): core ships no reconnect. The reconnect loop, <c>Last-Event-ID</c> and the request that
/// carries it are the caller's; the SDK only surfaces the <c>retry</c> hint and each event's <c>id</c>. Two scans keep it
/// so: no type in core holds a <c>Last-Event-ID</c> literal, and no type in <c>Dexpace.Sdk.Core.ServerSentEvents</c>
/// refers to a transport, to <c>HttpPipeline</c>, or to a <c>Send</c> or <c>SendAsync</c> member, so no SSE type can send
/// a request at all. Like SSE-37's, the scanners are proven against fixtures.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Sse38ArchitectureTests
{
    private const string SseNamespace = "Dexpace.Sdk.Core.ServerSentEvents";

    private static readonly Type[] s_sendingTypes =
        [typeof(IHttpClient), typeof(IAsyncHttpClient), typeof(HttpPipeline), typeof(DelegateHttpClient)];

    public static TheoryData<Type> SendingFixtures =>
    [
        typeof(SignatureHoldsClient),
        typeof(BodyCallsTransport),
        typeof(BodyCallsPipelineSendAsync),
        typeof(BodyCallsPipelineSend),
        typeof(GenericConstraintOnClient<>),
    ];

    [Fact]
    public void The_SSE_namespace_is_populated()
    {
        // Guards against the gate going inert: a rename of the namespace would leave both scans with nothing to scan.
        var names = SseTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ServerSentEvent", names);
        Assert.Contains("ServerSentEventReader", names);
        Assert.Contains("ServerSentEventStream", names);
        Assert.Contains("SseMapResult", names);
        Assert.Contains("ServerSentEventLineTooLongException", names);
    }

    [Fact]
    public void No_core_type_holds_a_Last_Event_ID_literal()
    {
        var offenders = CoreTypes()
            .Where(type => LastEventIdLiterals(type).Any())
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_Last_Event_ID_scan_sees_the_literal_in_any_case()
    {
        Assert.NotEmpty(LastEventIdLiterals(typeof(HeaderSetter)));
        Assert.NotEmpty(LastEventIdLiterals(typeof(LowerCaseHeaderSetter)));
        Assert.Empty(LastEventIdLiterals(typeof(CleanFixture)));
    }

    [Fact]
    public void No_SSE_type_references_a_transport_or_the_pipeline()
    {
        var violations = SseTypes()
            .SelectMany(type => SendingReferences(type).Select(reason => $"{type.FullName}: {reason}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(SendingFixtures))]
    public void The_sending_scan_sees_a_reference_in_each_place_one_can_hide(Type fixture) =>
        Assert.NotEmpty(SendingReferences(fixture));

    [Fact]
    public void The_sending_scan_ignores_a_type_that_never_sends() =>
        Assert.Empty(SendingReferences(typeof(CleanFixture)));

    private static Type[] CoreTypes() => typeof(Request).Assembly.GetTypes();

    private static Type[] SseTypes() =>
        [.. CoreTypes().Where(type => type.Namespace is { } ns && (ns == SseNamespace || ns.StartsWith(SseNamespace + ".", StringComparison.Ordinal)))];

    private static IEnumerable<string> LastEventIdLiterals(Type type) =>
        TypeReferences.StringLiterals(type).Where(literal => literal.Contains("Last-Event-ID", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SendingReferences(Type type)
    {
        foreach (var referenced in TypeReferences.Of(type).Where(s_sendingTypes.Contains))
        {
            yield return $"refers to {referenced.Name}";
        }

        foreach (var used in TypeReferences.UsedMembers(type).Where(member => member.Name is "Send" or "SendAsync"))
        {
            yield return $"uses {used.DeclaringType?.Name}.{used.Name}";
        }
    }

    // ── Scanner fixtures: a sending reference in each place one can hide, and one type with none ─────────────

    private sealed class SignatureHoldsClient
    {
        public IHttpClient? Client { get; init; }
    }

    private static class BodyCallsTransport
    {
        public static Task<Response> Go(IAsyncHttpClient client, Request request) =>
            client.ExecuteAsync(request, new RequestOptions(), CancellationToken.None);
    }

    private static class BodyCallsPipelineSendAsync
    {
        public static ValueTask<Response> Go(HttpPipeline pipeline, Request request) =>
            pipeline.SendAsync(request, CancellationToken.None);
    }

    private static class BodyCallsPipelineSend
    {
        public static Response Go(HttpPipeline pipeline, Request request) => pipeline.Send(request, CancellationToken.None);
    }

    private sealed class GenericConstraintOnClient<T>
        where T : IAsyncHttpClient
    {
        public int Count { get; init; }
    }

    private static class HeaderSetter
    {
        public static string Name() => "Last-Event-ID";
    }

    private static class LowerCaseHeaderSetter
    {
        public static string Name() => "last-event-id";
    }

    private static class CleanFixture
    {
        public static int Twice(int value) => value * 2;
    }
}
