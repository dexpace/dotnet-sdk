// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Source;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

/// <summary>
/// SEAM-29, HTTP-2 to HTTP-4 (phase 11, task 2.5; design §10 entry 10): a missing value throws an <see cref="ArgumentNullException"/> that names the field.
/// An <c>init</c> accessor's parameter is called <c>value</c> whatever the property is, so a bare <c>ThrowIfNull(value)</c> reported <c>ParamName == "value"</c>,
/// which names nothing; every validating accessor passes <c>nameof(Property)</c>. The table below holds one row per accessor of core and the conformance
/// kit; the reference transport's two are in its own test project. <see cref="The_table_covers_every_validating_accessor"/> re-scans the sources, so a new
/// accessor cannot slip in without a row.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InitAccessorParamNameTests
{
    // The accessors in src/Dexpace.Sdk.Http.SystemNet, covered by SystemNetHttpClientOptionsTests (this project does not reference the transport).
    private static readonly string[] s_coveredInTheTransportTests = ["SystemNetHttpClientOptions.Logger", "SystemNetHttpClientOptions.TimeProvider"];

    private static readonly Dictionary<string, Action> s_rows = new(StringComparer.Ordinal)
    {
        ["AuthRequirement.Scopes"] = () => _ = new AuthRequirement(AuthScheme.Basic) { Scopes = null! },
        ["AuthRequirement.Parameters"] = () => _ = new AuthRequirement(AuthScheme.Basic) { Parameters = null! },
        ["DexpaceClientOptions.UserAgent"] = () => _ = new DexpaceClientOptions { UserAgent = null! },
        ["DexpaceClientOptions.Retry"] = () => _ = new DexpaceClientOptions { Retry = null! },
        ["DexpaceClientOptions.Redirect"] = () => _ = new DexpaceClientOptions { Redirect = null! },
        ["DexpaceClientOptions.Logging"] = () => _ = new DexpaceClientOptions { Logging = null! },
        ["ProxyOptions.Host"] = () => _ = new ProxyOptions { Host = null!, Port = 8080 },
        ["ProxyOptions.NonProxyHosts"] = () => _ = new ProxyOptions { Host = "proxy.example", Port = 8080, NonProxyHosts = null! },
        ["ProxyOptions.NonProxyHosts[]"] = () => _ = new ProxyOptions { Host = "proxy.example", Port = 8080, NonProxyHosts = ["a.example", null!] },
        ["RedirectOptions.AllowedMethods"] = () => _ = new RedirectOptions { AllowedMethods = null! },
        ["RedirectOptions.AllowedMethods[]"] = () => _ = new RedirectOptions { AllowedMethods = new HashSet<Method> { null! } },
        ["RetryOptions.PacingHeaders"] = () => _ = new RetryOptions { PacingHeaders = null! },
        ["RetryOptions.RetryableStatusCodes"] = () => _ = new RetryOptions { RetryableStatusCodes = null! },
        ["RequestOptions.Tags"] = () => _ = new RequestOptions { Tags = null! },
        ["OperationDescriptor.Method"] = () => _ = new OperationDescriptor { Method = null!, PathTemplate = "/items" },
        ["OperationDescriptor.PathTemplate"] = () => _ = new OperationDescriptor { Method = Method.Get, PathTemplate = null! },
        ["OperationDescriptor.PathParameters"] = () => _ = new OperationDescriptor { Method = Method.Get, PathTemplate = "/items", PathParameters = null! },
        ["OperationDescriptor.Query"] = () => _ = new OperationDescriptor { Method = Method.Get, PathTemplate = "/items", Query = null! },
        ["OperationDescriptor.Headers"] = () => _ = new OperationDescriptor { Method = Method.Get, PathTemplate = "/items", Headers = null! },
        ["IdempotencyKeyStep.Methods"] = () => _ = new IdempotencyKeyStep { Methods = null! },
        ["FetchedPage.Page"] = () => _ = new FetchedPage<int>(new Page<int>([1], Status.Ok, Headers.Empty, Request.Get("https://example.test/"))) with { Page = null! },
        ["CodecSubject.CreateSerde"] = () => _ = new CodecSubject { CreateSerde = null! },
        ["InvariantSubject.CreatePipeline"] = () => _ = new InvariantSubject { CreatePipeline = null! },
        ["InvariantSubject.CreateResponseException"] = () => _ = new InvariantSubject { CreateResponseException = null! },
        ["InvariantSubject.EnumerateCauses"] = () => _ = new InvariantSubject { EnumerateCauses = null! },
        ["InvariantSubject.CreateTokenCache"] = () => _ = new InvariantSubject { CreateTokenCache = null! },
        ["InvariantSubject.RedactUrl"] = () => _ = new InvariantSubject { RedactUrl = null! },
        ["InvariantSubject.RedactHeaderValue"] = () => _ = new InvariantSubject { RedactHeaderValue = null! },
        ["InvariantSubject.Models"] = () => _ = new InvariantSubject { Models = null! },
        ["InvariantSubject.Auth"] = () => _ = new InvariantSubject { Auth = null! },
        ["InvariantSubject.Credentials"] = () => _ = new InvariantSubject { Credentials = null! },
        ["InvariantSubject.CreateDisposables"] = () => _ = new InvariantSubject { CreateDisposables = null! },
        ["InvariantSubject.AsAsync"] = () => _ = new InvariantSubject { AsAsync = null! },
        ["InvariantSubject.AsBlocking"] = () => _ = new InvariantSubject { AsBlocking = null! },
        ["PackagingSubject.PackagePaths"] = () => _ = new PackagingSubject { PackagePaths = null! },
        ["PackagingSubject.Policies"] = () => _ = new PackagingSubject { Policies = null! },
        ["SuiteOptions.Waivers"] = () => _ = new SuiteOptions { Waivers = null! },
        ["TransportSubject.Name"] = () => _ = new TransportSubject { Name = null! },
        ["TransportSuiteOptions.Waivers"] = () => _ = new TransportSuiteOptions { Waivers = null! },
    };

    public static TheoryData<string> Accessors => new(s_rows.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Accessors))]
    public void A_null_value_names_the_property(string accessor)
    {
        var thrown = Assert.Throws<ArgumentNullException>(s_rows[accessor]);

        Assert.Equal(accessor[(accessor.IndexOf('.', StringComparison.Ordinal) + 1)..].Replace("[]", string.Empty, StringComparison.Ordinal), thrown.ParamName);
    }

    [Fact]
    public void The_table_covers_every_validating_accessor()
    {
        var sites = InitAccessorScanner.Find(SourceTreeRoot()).Select(static site => site.Key).Except(s_coveredInTheTransportTests, StringComparer.Ordinal);

        Assert.Equal(sites.Order(StringComparer.Ordinal), s_rows.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_validating_accessor_in_the_libraries_names_its_property()
    {
        var unnamed = InitAccessorScanner.Find(SourceTreeRoot()).Where(static site => !site.NamesItsProperty).Select(static site => $"{site.File}:{site.Line} ({site.Key})");

        Assert.Empty(unnamed);
        Assert.True(InitAccessorScanner.Find(SourceTreeRoot()).Count >= 30, "the scan found almost no accessors; it is not looking at the sources");
    }

    [Fact]
    public void The_scanner_flags_a_bare_ThrowIfNull_and_ignores_a_method_parameter()
    {
        var directory = Directory.CreateTempSubdirectory("accessors");
        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "Bare.cs"),
                """
                public sealed class Bare
                {
                    public string Name
                    {
                        get;
                        init
                        {
                            ArgumentNullException.ThrowIfNull(value);
                            field = value;
                        }
                    }

                    public string Named
                    {
                        get;
                        init
                        {
                            ArgumentNullException.ThrowIfNull(value, nameof(Named));
                            field = value;
                        }
                    }

                    public IReadOnlyList<string> Items
                    {
                        get;
                        init
                        {
                            ArgumentNullException.ThrowIfNull(value, nameof(Items));
                            foreach (var item in value)
                            {
                                ArgumentNullException.ThrowIfNull(item, nameof(value));
                            }

                            field = value;
                        }
                    }

                    public string Arrow
                    {
                        get;
                        init => field = value ?? throw new ArgumentNullException(nameof(value));
                    }

                    public void Method(string value)
                    {
                        ArgumentNullException.ThrowIfNull(value);
                    }
                }
                """);

            var sites = InitAccessorScanner.Find(directory.FullName);

            Assert.Equal(["Bare.Name", "Bare.Named", "Bare.Items", "Bare.Items[]", "Bare.Arrow"], sites.Select(static site => site.Key));
            Assert.Equal([false, true, true, false, false], sites.Select(static site => site.NamesItsProperty));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string SourceTreeRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(Path.Combine(candidate, "Dexpace.Sdk.Core")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The src tree was not found above the test output.");
    }
}
