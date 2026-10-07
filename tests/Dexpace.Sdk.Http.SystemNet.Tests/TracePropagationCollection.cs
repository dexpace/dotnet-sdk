// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The collection of the trace-propagation wire tests: they install process-wide listeners and read headers that depend
/// on an ambient activity, so they run apart from the rest of the project.
/// </summary>
[CollectionDefinition("TracePropagation", DisableParallelization = true)]
public sealed class TracePropagationDefinition;
