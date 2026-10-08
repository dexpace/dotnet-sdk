// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>Scripted redirect responses for the policy suite.</summary>
internal static class RedirectFixtures
{
    /// <summary>A redirect that carries several <c>Location</c> values.</summary>
    internal static Response MultiLocation(params string[] values)
    {
        var headers = new Headers.Builder();
        foreach (var value in values)
        {
            headers.AddInbound("Location", value);
        }

        return TestResponses.Create(Status.Found, headers: headers.Build());
    }

    /// <summary>A scripted chain: <paramref name="hops"/> distinct 302 responses (<c>/h0</c> to <c>/h{n-1}</c>), then a 200.</summary>
    internal static object[] Chain(int hops, string host = "api.example.com")
    {
        var script = new object[hops + 1];
        for (var i = 0; i < hops; i++)
        {
            var target = $"https://{host}/h{i}";
            script[i] = TestResponses.Redirect(302, target);
        }

        script[hops] = TestResponses.Create(Status.Ok);
        return script;
    }

    /// <summary>A 307 whose body records its dispose into <paramref name="log"/> under <paramref name="name"/>.</summary>
    internal static Response TrackedRedirect(string location, List<string> log, string name, int status = 307) =>
        TestResponses.Create(
            Status.FromCode(status),
            headers: new Headers.Builder().Set("Location", location).Build(),
            body: new TrackingResponseBody(log, name));
}
