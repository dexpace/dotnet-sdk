// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Source;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

/// <summary>
/// SEAM-29, HTTP-2 to HTTP-4 (phase 12, task 1.2, P12-14; design V11 and its amendment): a value an <c>init</c> accessor rejects for its range or its format
/// names the property in <see cref="ArgumentException.ParamName"/>, as a null already does (<see cref="InitAccessorParamNameTests"/>). The defect had two spellings,
/// <c>nameof(value)</c> passed to an exception and a framework throw helper called on <c>value</c> (whose <c>ParamName</c> is the argument expression, so
/// <c>"value"</c> again); both made the exception name nothing. The table below holds one row per rejecting accessor of core and the conformance kit, keyed
/// <c>Type.Property</c>; a <c> (null)</c> suffix selects the null half of <c>ThrowIfNullOrWhiteSpace</c>, and any other suffix a second row for the same property.
/// The rows the scanner sees are checked for coverage by <see cref="The_table_covers_every_rejecting_accessor"/>; the rows after the marker comment are the accessors
/// that route through a helper method (<c>RequireDuration</c>, <c>RequireNullOrPositive</c>, <c>RequireNullOrNonNegative</c>, <c>RequireUsable</c>,
/// <c>RequireTimeout</c>; and <c>ServerSentEvent</c>'s <c>RejectNul</c>, <c>RejectBadRetry</c> and <c>CopyOf</c> and <c>HttpLoggingOptions</c>' <c>Copy</c>, which the
/// phase 12 review found still naming <c>"value"</c> and <c>"source"</c>), which a line scan cannot see, kept as behavioural rows. The reference transport's three are
/// in its own test project.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InitAccessorRejectionParamNameTests
{
    // The rejecting accessors of src/Dexpace.Sdk.Http.SystemNet, covered by its InitAccessorParamNameTests (this project does not reference the transport).
    private static readonly string[] s_coveredInTheTransportTests =
    [
        "SystemNetHttpClientOptions.Timeout",
        "SystemNetHttpClientOptions.HeaderDropLogging",
        "SystemNetHttpClientOptions.ResendBufferLimit",
    ];

    private static readonly Dictionary<string, Action> s_rows = new(StringComparer.Ordinal)
    {
        // The rows the scanner sees: nameof(value) and the framework throw helpers called on value.
        ["OperationDescriptor.OperationId"] = () => _ = new OperationDescriptor { Method = Method.Get, PathTemplate = "/items", OperationId = " " },
        ["ProxyOptions.Type"] = () => _ = new ProxyOptions { Host = "proxy.example", Port = 8080, Type = (ProxyType)99 },
        ["ProxyOptions.Host"] = () => _ = new ProxyOptions { Host = " ", Port = 8080 },
        ["ProxyOptions.Port"] = () => _ = new ProxyOptions { Host = "proxy.example", Port = 70000 },
        ["RetryOptions.Multiplier"] = () => _ = new RetryOptions { Multiplier = 0.5 },
        ["RetryOptions.Jitter"] = () => _ = new RetryOptions { Jitter = 1.5 },
        ["RetryOptions.RetryableStatusCodes"] = () => _ = new RetryOptions { RetryableStatusCodes = new HashSet<int> { 200 } },
        ["RetryOptions.AttemptHeaderName"] = () => _ = new RetryOptions { AttemptHeaderName = "not a token" },
        ["RetryOptions.AttemptHeaderName (idempotency key)"] = () => _ = new RetryOptions { AttemptHeaderName = "Idempotency-Key" },
        ["RetryOptions.MaxRetryAttempts"] = () => _ = new RetryOptions { MaxRetryAttempts = -1 },
        ["AuthRequirement.Scopes"] = () => _ = new AuthRequirement(AuthScheme.Basic) { Scopes = [null!] },
        ["TransportSubject.Name"] = () => _ = new TransportSubject { Name = " " },
        ["HttpLoggingOptions.BodyPreviewSize"] = () => _ = new HttpLoggingOptions { BodyPreviewSize = -1 },
        ["RedirectOptions.MaxRedirects"] = () => _ = new RedirectOptions { MaxRedirects = -1 },
        ["AccessTokenCache.RefreshMargin"] = () => _ = new AccessTokenCache(new StubCredential()) { RefreshMargin = TimeSpan.FromSeconds(-1) },
        ["AuthCredentials.TokenRefreshMargin"] = () => _ = new AuthCredentials { TokenRefreshMargin = TimeSpan.FromSeconds(-1) },
        ["CodecSubject.Name"] = () => _ = new CodecSubject { Name = " " },
        ["CodecSubject.Name (null)"] = () => _ = new CodecSubject { Name = null! },
        ["InvariantSubject.Name"] = () => _ = new InvariantSubject { Name = " " },
        ["InvariantSubject.Name (null)"] = () => _ = new InvariantSubject { Name = null! },
        ["PackagingSubject.Name"] = () => _ = new PackagingSubject { Name = " " },
        ["PackagingSubject.Name (null)"] = () => _ = new PackagingSubject { Name = null! },
        ["PackagingSubject.CoreId"] = () => _ = new PackagingSubject { CoreId = " " },
        ["PackagingSubject.CoreId (null)"] = () => _ = new PackagingSubject { CoreId = null! },
        ["PackagingSubject.DeclaredFloor"] = () => _ = new PackagingSubject { DeclaredFloor = " " },
        ["PackagingSubject.DeclaredFloor (null)"] = () => _ = new PackagingSubject { DeclaredFloor = null! },
        ["TransportSuiteOptions.AssertionTimeout"] = () => _ = new TransportSuiteOptions { AssertionTimeout = TimeSpan.Zero },
        ["TransportSuiteOptions.ReleaseTimeout"] = () => _ = new TransportSuiteOptions { ReleaseTimeout = TimeSpan.Zero },
        ["SuiteOptions.AssertionTimeout"] = () => _ = new SuiteOptions { AssertionTimeout = TimeSpan.Zero },

        // The helper-routed rows the scanner cannot see: each helper takes the property's name, and these rows pin that it is passed.
        ["RetryOptions.BaseDelay"] = () => _ = new RetryOptions { BaseDelay = TimeSpan.FromSeconds(-1) },
        ["RetryOptions.MaxDelay"] = () => _ = new RetryOptions { MaxDelay = TimeSpan.FromSeconds(-1) },
        ["RetryOptions.FixedDelay"] = () => _ = new RetryOptions { FixedDelay = TimeSpan.FromSeconds(-1) },
        ["RequestOptions.Timeout"] = () => _ = new RequestOptions { Timeout = TimeSpan.Zero },
        ["RequestOptions.MaxRetries"] = () => _ = new RequestOptions { MaxRetries = -1 },
        ["DexpaceClientOptions.BaseAddress"] = () => _ = new DexpaceClientOptions { BaseAddress = new Uri("/relative", UriKind.Relative) },
        ["DexpaceClientOptions.OverallTimeout"] = () => _ = new DexpaceClientOptions { OverallTimeout = TimeSpan.Zero },
        ["DexpaceClientOptions.AttemptTimeout"] = () => _ = new DexpaceClientOptions { AttemptTimeout = TimeSpan.Zero },
        ["ServerSentEvent.Id"] = () => _ = new ServerSentEvent { Id = "a\0b" },
        ["ServerSentEvent.Retry"] = () => _ = new ServerSentEvent { Retry = TimeSpan.FromMilliseconds(-1) },
        ["ServerSentEvent.Data (null)"] = () => _ = new ServerSentEvent { Data = null! },
        ["ServerSentEvent.Data (null element)"] = () => _ = new ServerSentEvent { Data = ["a", null!] },
        ["HttpLoggingOptions.AllowedHeaderNames (null)"] = () => _ = new HttpLoggingOptions { AllowedHeaderNames = null! },
        ["HttpLoggingOptions.AllowedHeaderNames (null element)"] = () => _ = new HttpLoggingOptions { AllowedHeaderNames = ["accept", null!] },
        ["HttpLoggingOptions.AllowedQueryParameters (null)"] = () => _ = new HttpLoggingOptions { AllowedQueryParameters = null! },
        ["HttpLoggingOptions.AllowedQueryParameters (null element)"] = () => _ = new HttpLoggingOptions { AllowedQueryParameters = ["api-version", null!] },
        ["HttpLoggingOptions.UrlValuedHeaderNames (null)"] = () => _ = new HttpLoggingOptions { UrlValuedHeaderNames = null! },
        ["HttpLoggingOptions.UrlValuedHeaderNames (null element)"] = () => _ = new HttpLoggingOptions { UrlValuedHeaderNames = ["location", null!] },
    };

    public static TheoryData<string> Accessors => new(s_rows.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Accessors))]
    public void A_rejected_value_names_the_property(string row)
    {
        var thrown = Assert.ThrowsAny<ArgumentException>(s_rows[row]);

        Assert.Equal(PropertyOf(row), thrown.ParamName);
        if (row.EndsWith(" (null)", StringComparison.Ordinal))
        {
            Assert.IsType<ArgumentNullException>(thrown);
        }
    }

    [Fact]
    public void The_table_covers_every_rejecting_accessor()
    {
        var rejecting = InitAccessorScanner.Find(SourceTreeRoot())
            .Where(static site => site.Kind == InitAccessorScanner.SiteKind.Rejection && !s_coveredInTheTransportTests.Contains(site.Key, StringComparer.Ordinal))
            .ToList();
        var keys = s_rows.Keys.Select(static row => KeyOf(row)).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(rejecting.Where(site => !keys.Contains(site.Key)).Select(static site => $"{site.File}:{site.Line} ({site.Key}) has no row"));
        Assert.Empty(rejecting.Where(site => site.RejectsNull && !s_rows.ContainsKey($"{site.Key} (null)")).Select(static site => $"{site.File}:{site.Line} ({site.Key}) rejects a null and has no ' (null)' row"));
        Assert.True(rejecting.Count >= 20, $"the scan found only {rejecting.Count} rejecting sites; it is not looking at the sources");
    }

    // `Type.Property`, `Type.Property (null)` and `Type.Property (anything)` all select the property.
    private static string KeyOf(string row) => row.Split(' ')[0];

    private static string PropertyOf(string row)
    {
        var key = KeyOf(row);
        return key[(key.IndexOf('.', StringComparison.Ordinal) + 1)..];
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

    private sealed class StubCredential : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            throw new NotSupportedException("The cache is never asked for a token here.");
    }
}
