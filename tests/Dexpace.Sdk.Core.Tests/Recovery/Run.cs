// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

/// <summary>Drives one body through either the synchronous or the asynchronous form, so a test is a <c>[Theory]</c> over <c>bool async</c>.</summary>
internal static class Run
{
    internal static async Task<Request> ApplyWith(RequestRecoveryChain chain, Request request, bool async, CancellationToken ct) =>
        async ? await chain.ApplyAsync(request, ct) : chain.Apply(request, ct);

    internal static async Task<Outcome> ApplyWith(ResponseRecoveryChain chain, Outcome outcome, bool async, CancellationToken ct) =>
        async ? await chain.ApplyAsync(outcome, ct) : chain.Apply(outcome, ct);

    internal static async Task<Response> ApplyWith(IResponseStep step, Response response, bool async, CancellationToken ct) =>
        async ? await step.ApplyAsync(response, ct) : step.Apply(response, ct);

    internal static async Task<Request> ApplyWith(IRequestStep step, Request request, bool async, CancellationToken ct) =>
        async ? await step.ApplyAsync(request, ct) : step.Apply(request, ct);

    internal static Task<Request> Apply(RequestRecoveryChain chain, Request request, bool async) =>
        ApplyWith(chain, request, async, TestContext.Current.CancellationToken);

    internal static Task<Outcome> Apply(ResponseRecoveryChain chain, Outcome outcome, bool async) =>
        ApplyWith(chain, outcome, async, TestContext.Current.CancellationToken);

    internal static Task<Response> Apply(IResponseStep step, Response response, bool async) =>
        ApplyWith(step, response, async, TestContext.Current.CancellationToken);

    internal static Task<Request> Apply(IRequestStep step, Request request, bool async) =>
        ApplyWith(step, request, async, TestContext.Current.CancellationToken);
}
