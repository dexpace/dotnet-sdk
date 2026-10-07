// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Support;

/// <summary>
/// The collection for tests that need the process to have no <c>Dexpace.Sdk</c> listener at all (the zero-allocation
/// assertions of OBS-1 and OBS-25). It disables parallelisation, so xUnit runs its classes alone: an
/// <see cref="System.Diagnostics.ActivityListener"/> or <see cref="System.Diagnostics.Metrics.MeterListener"/> is
/// process-wide, and a recorder in a parallel test would make a span exist or a measurement count (P5c-15).
/// </summary>
[CollectionDefinition("NoDiagnosticListeners", DisableParallelization = true)]
public sealed class NoDiagnosticListenersDefinition;
