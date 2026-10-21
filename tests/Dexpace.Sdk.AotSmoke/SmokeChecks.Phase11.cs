// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.AotSmoke;

// Phase 11 repairs the audited deviations (design §10); the repairs that touch code the trimmer and the AOT compiler could break run in the published binary.
// Each later pull request of the phase appends its checks here.
internal static partial class SmokeChecks
{
    private static async Task CheckPhase11Async()
    {
        // BODY-14 (task 3.1): a body hands out one stream, and a second open, in either form, returns that same stream.
        var body = ResponseBody.FromBytes("abc"u8.ToArray());
        var first = await body.OpenReadAsync();
        Expect(ReferenceEquals(first, body.OpenRead()), "a second open of a response body returns the same stream under NativeAOT");
        body.Dispose();

        // BODY-11, HTTP-40 (task 3.3): FromFile classifies a size-0 path through System.Formats.Tar without opening a special file, so a Unix character
        // device is refused at construction; the classification is trim- and AOT-clean (the publish prints no warning).
        if (OperatingSystem.IsLinux())
        {
            var rejected = false;
            try
            {
                _ = RequestBody.FromFile("/dev/null");
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Expect(rejected, "RequestBody.FromFile refuses /dev/null (a character device) under NativeAOT");
        }
    }
}
