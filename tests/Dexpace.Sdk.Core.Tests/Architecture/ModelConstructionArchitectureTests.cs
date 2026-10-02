// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// HTTP-2 and SEAM-29 (design §4, §10 <c>no-builder-objects</c> and <c>construction-bypass</c>): there are exactly four
/// construction routes, so the public constructors under <c>Dexpace.Sdk.Core.Http</c> are a closed list, value types
/// are made through factories, no generic builder contract exists, and a derived <c>Request</c> cannot bypass its
/// validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelConstructionArchitectureTests
{
    // The validating constructors (Request, Response), the parameterless records whose every init validates, and the
    // two multimap builders. Anything else under Http.* is a new construction route and needs a design decision.
    private static readonly string[] s_allowedConstructors =
    [
        "Dexpace.Sdk.Core.Http.Request.Request(Dexpace.Sdk.Core.Http.Common.Method, System.Uri, Dexpace.Sdk.Core.Http.Common.Headers, Dexpace.Sdk.Core.Http.Request.RequestBody)",
        "Dexpace.Sdk.Core.Http.Response.Response(Dexpace.Sdk.Core.Http.Request.Request, Dexpace.Sdk.Core.Http.Response.Status, Dexpace.Sdk.Core.Http.Common.Protocol, Dexpace.Sdk.Core.Http.Common.Headers, Dexpace.Sdk.Core.Http.Response.ResponseBody, System.String)",
        "Dexpace.Sdk.Core.Http.Request.RequestOptions()",
        "Dexpace.Sdk.Core.Http.Request.RequestConditions()",
        "Dexpace.Sdk.Core.Http.Common.Headers+Builder()",
        "Dexpace.Sdk.Core.Http.Request.Query+Builder()",
    ];

    public static TheoryData<Type> FactoryCreatedTypes() =>
    [
        typeof(Method),
        typeof(HttpHeaderName),
        typeof(MediaType),
        typeof(ETag),
        typeof(HttpRange),
        typeof(Query),
    ];

    [Fact]
    public void Public_constructors_in_Http_namespaces_are_exactly_the_allow_list()
    {
        var actual = typeof(Request).Assembly.GetExportedTypes()
            .Where(t => t.Namespace?.StartsWith("Dexpace.Sdk.Core.Http", StringComparison.Ordinal) == true)
            .Concat(typeof(Request).Assembly.GetExportedTypes().SelectMany(t => t.GetNestedTypes(BindingFlags.Public))
                .Where(t => t.Namespace?.StartsWith("Dexpace.Sdk.Core.Http", StringComparison.Ordinal) == true))
            .Distinct()
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            .Where(c => !c.DeclaringType!.IsValueType)
            .Select(Describe)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(s_allowedConstructors.Order(StringComparer.Ordinal), actual);
    }

    [Theory]
    [MemberData(nameof(FactoryCreatedTypes))]
    public void Value_types_are_created_through_factories(Type type)
    {
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(
            type.GetMethods(BindingFlags.Public | BindingFlags.Static),
            m => m.ReturnType == type || Nullable.GetUnderlyingType(m.ReturnType) == type);
    }

    [Fact]
    public void No_generic_builder_contract_exists()
    {
        // SEAM-29's 🚫: composition takes Func<T, T>; the shared generic Builder is not built (design §10 no-builder-objects).
        var offenders = typeof(Request).Assembly.GetExportedTypes()
            .SelectMany(t => new[] { t }.Concat(t.GetNestedTypes(BindingFlags.Public)))
            .Where(t => t.IsGenericTypeDefinition && (t.Name.StartsWith("IBuilder`", StringComparison.Ordinal) || t.Name.StartsWith("Builder`", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void A_derived_request_cannot_bypass_validation()
    {
        // The reflection form of RequestTests.Deriving_a_request_cannot_install_a_body_carrying_GET: `with` is not
        // compilable because the four properties have no setter at all, not even an init one, and every With* routes
        // through the validating constructor.
        foreach (var name in new[] { "Method", "Url", "Headers", "Body" })
        {
            var property = typeof(Request).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.Null(property.SetMethod);
        }

        var post = Request.Post("https://example.test/", RequestBody.FromString("{}"));
        Assert.Throws<ArgumentException>(() => post.WithMethod(Method.Get));
        Assert.Throws<ArgumentException>(() => Request.Get("https://example.test/").WithBody(RequestBody.FromString("{}")));
        Assert.Throws<ArgumentException>(() => post.WithUrl(new Uri("ftp://example.test/")));
    }

    private static string Describe(ConstructorInfo constructor) =>
        $"{constructor.DeclaringType!.FullName}({string.Join(", ", constructor.GetParameters().Select(p => p.ParameterType.FullName))})";
}
