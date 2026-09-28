// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.AotSmoke;

// Exit code 0 means every check held in the NativeAOT binary; anything else names the first failure.
return await SmokeChecks.RunAllAsync();
