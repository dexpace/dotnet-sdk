// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    // Predicate is code-only (a delegate); AllowedMethods replaces the default set (V1).
    private static RedirectOptions ReadRedirect(ConfigSection section)
    {
        var binder = new RecordBinder<RedirectOptions>(section, new RedirectOptions());
        binder.Int("Redirect:MaxRedirects", static (r, v) => r with { MaxRedirects = v });
        binder.Bool("Redirect:AllowHttpsToHttpDowngrade", static (r, v) => r with { AllowHttpsToHttpDowngrade = v });
        binder.Bool("Redirect:FollowSeeOther", static (r, v) => r with { FollowSeeOther = v });
        binder.Methods("Redirect:AllowedMethods", static (r, v) => r with { AllowedMethods = new HashSet<Method>(v) });
        return binder.Value;
    }
}
