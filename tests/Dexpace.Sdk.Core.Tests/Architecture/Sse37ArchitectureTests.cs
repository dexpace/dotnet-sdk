// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// SSE-37 (design §7.2, §9.2): the SSE core carries no serialization dependency, so no type in
/// <c>Dexpace.Sdk.Core.ServerSentEvents</c> refers to anything in <c>Dexpace.Sdk.Core.Serialization</c>.
/// </summary>
/// <remarks>
/// <para>
/// Wired but inert until the SSE namespace lands (roadmap constraint 1: phase 7 turns it on). There is nothing to
/// switch: the gate reports itself skipped while the namespace holds no type and enforces the rule, unchanged, from
/// the first type that lands there. The paging-engine half of the rule binds once phase 7 separates the engine from
/// the ISerde-taking factory (design §7.2); today's pagination takes an ISerde by design.
/// </para>
/// <para>
/// A second test keeps the inert state honest by failing on core types outside that namespace that show an SSE
/// signal: a namespace segment <c>Sse</c>, or one containing <c>ServerSent</c> or <c>EventStream</c>; a type name
/// starting <c>Sse</c> followed by an upper-case letter, digit or underscore (or exactly <c>Sse</c>), or containing
/// <c>ServerSent</c> or <c>EventStream</c>; a <c>text/event-stream</c> string literal in a method body; or a use of
/// <c>CommonMediaTypes.TextEventStream</c> by any type but <c>CommonMediaTypes</c> itself. It is a heuristic: SSE code
/// with none of these signals (say, a <c>LineFramer</c> that never names the media type) is not caught, and review
/// remains the backstop.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed partial class Sse37ArchitectureTests
{
    private const string SseNamespace = "Dexpace.Sdk.Core.ServerSentEvents";
    private const string SerializationNamespace = "Dexpace.Sdk.Core.Serialization";

    public static TheoryData<Type> ScannerViolations =>
    [
        typeof(SignatureReference),
        typeof(BodyReference),
        typeof(ConstrainedType<>),
        typeof(ConstrainedMethod),
        typeof(FieldAttributeReference),
        typeof(PropertyAttributeReference),
        typeof(EventAttributeReference),
        typeof(MethodAttributeReference),
        typeof(ParameterAttributeReference),
        typeof(ReturnAttributeReference),
    ];

    public static TheoryData<Type> SseLookalikes =>
    [
        typeof(SseEventReader),
        typeof(Sse),
        typeof(ChunkedEventStreamParser),
        typeof(ServerSentThing),
        typeof(MediaSniffer),
        typeof(MediaTypeUser),
    ];

    [Fact]
    public void Sse_types_reference_nothing_in_the_serialization_namespace()
    {
        var sseTypes = CoreTypes().Where(type => IsIn(type, SseNamespace)).ToArray();
        Assert.SkipWhen(
            sseTypes.Length == 0,
            $"SSE-37 is inert: {SseNamespace} holds no type yet. Roadmap phase 7 lands it, and this test then "
            + "enforces the rule with no edit.");

        var violations = sseTypes
            .SelectMany(type => SerializationReferences(type).Select(target => $"{type.FullName} -> {target.FullName}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Sse_code_lives_only_in_the_namespace_the_gate_watches()
    {
        var strays = CoreTypes()
            .Where(type => !IsIn(type, SseNamespace))
            .Select(type => (Type: type, Signal: SseSignal(type)))
            .Where(candidate => candidate.Signal is not null)
            .Select(candidate => $"{candidate.Type.FullName} ({candidate.Signal})")
            .ToArray();

        Assert.True(
            strays.Length == 0,
            $"SSE code outside {SseNamespace} escapes the SSE-37 gate; move it there or retarget the gate: "
            + string.Join(", ", strays));
    }

    // The gate is only as good as its scanner, and it has nothing to scan until phase 7: prove it here, once per
    // place a reference can hide.
    [Theory]
    [MemberData(nameof(ScannerViolations))]
    public void Scanner_sees_a_serialization_reference(Type fixture) =>
        Assert.Contains(typeof(ISerde), SerializationReferences(fixture));

    [Fact]
    public void Scanner_sees_nothing_in_a_clean_type() =>
        Assert.Empty(SerializationReferences(typeof(CleanReference)));

    [Theory]
    [MemberData(nameof(SseLookalikes))]
    public void Stray_guard_flags_an_sse_signal(Type fixture) => Assert.NotNull(SseSignal(fixture));

    [Theory]
    [InlineData(typeof(CleanReference))]
    [InlineData(typeof(Session))]
    [InlineData(typeof(Assessor))]
    [InlineData(typeof(Ssemantic))]
    [InlineData(typeof(CommonMediaTypes))]
    public void Stray_guard_ignores_a_type_without_one(Type fixture) => Assert.Null(SseSignal(fixture));

    [Theory]
    [InlineData("Dexpace.Sdk.Core.Sse", true)]
    [InlineData("Dexpace.Sdk.Core.ServerSent", true)]
    [InlineData("Dexpace.Sdk.Core.Streaming.EventStreams", true)]
    [InlineData("Dexpace.Sdk.Core.Session", false)]
    [InlineData("Dexpace.Sdk.Core.Http.Common", false)]
    public void Stray_guard_reads_namespace_segments(string ns, bool flagged) =>
        Assert.Equal(flagged, NamespaceLooksLikeSse(ns));

    private static Type[] CoreTypes() => typeof(Request).Assembly.GetTypes();

    private static IEnumerable<Type> SerializationReferences(Type type) =>
        TypeReferences.Of(type).Where(target => IsIn(target, SerializationNamespace));

    private static bool IsIn(Type type, string ns) =>
        type.Namespace is { } name && (name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal));

    /// <summary>The first SSE signal <paramref name="type"/> shows, or <see langword="null"/>.</summary>
    private static string? SseSignal(Type type)
    {
        if (NamespaceLooksLikeSse(type.Namespace))
        {
            return "namespace";
        }

        var name = type.Name.TrimStart('<');
        if (SsePrefix().IsMatch(name)
            || name.Contains("ServerSent", StringComparison.OrdinalIgnoreCase)
            || name.Contains("EventStream", StringComparison.OrdinalIgnoreCase))
        {
            return "type name";
        }

        if (TypeReferences.StringLiterals(type).Any(
            literal => literal.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)))
        {
            return "text/event-stream literal";
        }

        var textEventStream = typeof(CommonMediaTypes).GetProperty(nameof(CommonMediaTypes.TextEventStream))!.GetMethod;
        return type != typeof(CommonMediaTypes) && TypeReferences.UsedMembers(type).Contains(textEventStream)
            ? "uses CommonMediaTypes.TextEventStream"
            : null;
    }

    private static bool NamespaceLooksLikeSse(string? ns) =>
        (ns ?? string.Empty).Split('.').Any(segment =>
            segment.Equals("Sse", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("ServerSent", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("EventStream", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("^Sse([A-Z0-9_`]|$)")]
    private static partial Regex SsePrefix();

    // ── Scanner fixtures: a Serialization reference in each place one can hide, and one type with none ────────

    [AttributeUsage(AttributeTargets.All)]
    private sealed class MarkerAttribute(Type target) : Attribute
    {
        public Type Target { get; } = target;
    }

    private sealed class SignatureReference
    {
        public ISerde? Serde { get; init; }
    }

    private static class BodyReference
    {
        public static MediaType? Probe(object candidate) => (candidate as ISerde)?.DefaultMediaType;
    }

    private sealed class ConstrainedType<T>
        where T : ISerde
    {
        public int Count { get; init; }
    }

    private static class ConstrainedMethod
    {
        public static int Count<T>()
            where T : ISerde => 0;
    }

    private static class FieldAttributeReference
    {
        [Marker(typeof(ISerde))]
        public static readonly int Field = 1;
    }

    private static class PropertyAttributeReference
    {
        [Marker(typeof(ISerde))]
        public static int Property => 1;
    }

    private static class EventAttributeReference
    {
        [Marker(typeof(ISerde))]
        public static event EventHandler Changed
        {
            add { }
            remove { }
        }
    }

    private static class MethodAttributeReference
    {
        [Marker(typeof(ISerde))]
        public static int Method() => 1;
    }

    private static class ParameterAttributeReference
    {
        public static int Method([Marker(typeof(ISerde))] int value) => value;
    }

    private static class ReturnAttributeReference
    {
        [return: Marker(typeof(ISerde))]
        public static int Method() => 1;
    }

    private static class CleanReference
    {
        public static int Twice(int value) => value * 2;
    }

    // ── Stray-guard fixtures ─────────────────────────────────────────────────────────────────────────────────

    private sealed class SseEventReader;

    private sealed class Sse;

    private sealed class ChunkedEventStreamParser;

    private sealed class ServerSentThing;

    private static class MediaSniffer
    {
        public static bool IsSse(string contentType) => contentType.StartsWith("text/event-stream", StringComparison.Ordinal);
    }

    private static class MediaTypeUser
    {
        public static MediaType Accept() => CommonMediaTypes.TextEventStream;
    }

    private sealed class Session;

    private sealed class Assessor;

    private sealed class Ssemantic;
}
