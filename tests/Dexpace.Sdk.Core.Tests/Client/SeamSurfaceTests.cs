// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// The shape of the transport SPI (SEAM-11, SEAM-13, SEAM-17; design §3.2, §3.3, fact 2): <c>Task&lt;Response&gt;</c>
/// for the async pivot, a token and options on both, and no parameter defaults, because a default on an implementation's
/// three-parameter member would let <c>transport.ExecuteAsync(request, default)</c> bind it with null options.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SeamSurfaceTests
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void The_async_seam_returns_Task_of_Response()
    {
        var method = typeof(IAsyncHttpClient).GetMethod(nameof(IAsyncHttpClient.ExecuteAsync))!;

        Assert.Equal(typeof(Task<Response>), method.ReturnType);
        Assert.NotEqual(typeof(ValueTask<Response>), method.ReturnType);
    }

    [Fact]
    public void The_sync_seam_takes_a_token_and_options()
    {
        var sync = typeof(IHttpClient).GetMethod(nameof(IHttpClient.Execute))!;
        var asynchronous = typeof(IAsyncHttpClient).GetMethod(nameof(IAsyncHttpClient.ExecuteAsync))!;
        Type[] expected = [typeof(Request), typeof(RequestOptions), typeof(CancellationToken)];

        Assert.Equal(typeof(Response), sync.ReturnType);
        Assert.Equal(expected, sync.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(expected, asynchronous.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void No_SPI_member_declares_a_default_on_any_parameter()
    {
        Type[] seamTypes =
        [
            typeof(IHttpClient),
            typeof(IAsyncHttpClient),
            typeof(RecordingTransport),
            typeof(RecordingSyncTransport),
            typeof(ScriptedTransport),
            .. typeof(HttpClientExtensions).Assembly.GetTypes()
                .Where(t => !t.IsInterface && (typeof(IHttpClient).IsAssignableFrom(t) || typeof(IAsyncHttpClient).IsAssignableFrom(t))),
        ];

        var offenders = seamTypes.Distinct()
            .SelectMany(type => type.GetMethods(AllDeclared).Select(method => (type, method)))
            .Where(pair => pair.method.Name is "Execute" or "ExecuteAsync"
                && pair.method.GetParameters().Length == 3
                && pair.method.GetParameters().Any(p => p.HasDefaultValue))
            .Select(pair => $"{pair.type.Name}.{pair.method.Name}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "A default on a three-parameter SPI member makes transport.ExecuteAsync(request, default) bind it with null options (design fact 2): "
            + string.Join(", ", offenders));
    }
}
