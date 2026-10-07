// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

[Trait("Category", "Unit")]
public sealed class AttemptScopeTests
{
    private static readonly UrlRedactor s_redactor = new();

    private static (Request Request, PipelineContext Context) Make(string url)
    {
        var request = Request.Get(url);
        return (request, TestContexts.For(request));
    }

    [Fact]
    public void Construction_allocates_nothing()
    {
        var (request, context) = Make("https://h/p?a=1");
        Measure(request, context); // warm up
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            Measure(request, context);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static long Measure(Request request, PipelineContext context)
    {
        var scope = new AttemptScope(request, context, s_redactor);
        return scope.StartTimestamp;
    }

    [Fact]
    public void The_redacted_url_is_not_computed_until_read_and_is_computed_once()
    {
        var (request, context) = Make("https://h/p?a=1&api-version=2");
        var scope = new AttemptScope(request, context, s_redactor);

        var first = scope.RedactedUrl;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var second = scope.RedactedUrl;

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(first, second);
        Assert.Equal("https://h/p?a=***&api-version=2", first);
    }

    [Fact]
    public void The_cached_url_survives_being_passed_by_ref()
    {
        var (request, context) = Make("https://h/p?a=1");
        var scope = new AttemptScope(request, context, s_redactor);

        var viaRef = ReadByRef(ref scope);

        Assert.Same(viaRef, scope.RedactedUrl);
    }

    private static string ReadByRef(ref AttemptScope scope) => scope.RedactedUrl;

    [Fact]
    public void The_redacted_url_uses_the_supplied_redactor()
    {
        var (request, context) = Make("https://h/p?keep=1&drop=2");
        var scope = new AttemptScope(request, context, new UrlRedactor(["keep"]));

        Assert.Equal("https://h/p?keep=1&drop=***", scope.RedactedUrl);
    }

    [Fact]
    public async Task Elapsed_is_measured_from_construction_and_frozen_by_Stop()
    {
        var (request, context) = Make("https://h/p");
        var scope = new AttemptScope(request, context, s_redactor);

        var first = scope.Elapsed;
        await Task.Delay(5, TestContext.Current.CancellationToken);
        var second = scope.Elapsed;
        scope.Stop();
        var frozen = scope.Elapsed;
        await Task.Delay(5, TestContext.Current.CancellationToken);

        Assert.True(first >= TimeSpan.Zero);
        Assert.True(second >= first);
        Assert.Equal(frozen, scope.Elapsed);
    }

    [Fact]
    public void Method_and_attempt_number_are_captured()
    {
        var (request, context) = Make("https://h/p");
        var scope = new AttemptScope(request, context.ForAttempt(3), s_redactor);

        Assert.Equal("GET", scope.MethodName);
        Assert.Equal(3, scope.AttemptNumber);
    }
}
