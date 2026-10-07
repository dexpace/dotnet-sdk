// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>Shared builders for the execution-context tests.</summary>
internal static class ExecutionTestSupport
{
    internal static Request NewRequest() => new(Method.Get, new Uri("https://api.example.test/widgets"));

    internal static Response NewResponse(Request? request = null, ResponseBody? body = null) =>
        new(request ?? NewRequest(), Status.Ok, Protocol.Http11, body: body);

    internal static InstrumentationContext NewBundle() =>
        InstrumentationContext.FromContext(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

    internal static DispatchContext NewDispatch(ContextStore store, CallKey? key = null, InstrumentationContext? bundle = null) =>
        new(bundle, key, store);
}
