// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>AUTH-37, OBS-39: the one auth log event.</summary>
[Trait("Category", "Unit")]
public sealed class AuthLogTests
{
    [Fact]
    public void TokenRefreshFailed_is_id_160_named_dexpace_auth_token_refresh_failed_at_Warning()
    {
        var logger = new RecordingLogger();
        var failure = new InvalidOperationException("down");

        AuthLog.TokenRefreshFailed(logger, failure);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(160, entry.EventId.Id);
        Assert.Equal("dexpace.auth.token_refresh_failed", entry.EventId.Name);
        Assert.Equal(DexpaceLogEvents.TokenRefreshFailedId, entry.EventId.Id);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("System.InvalidOperationException", entry["error.type"]);
        Assert.Same(failure, entry.Exception);
        Assert.DoesNotContain("down", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_throwing_logger_does_not_throw()
    {
        var logger = new ThrowingLogger { ThrowOnLog = true };

        var thrown = Record.Exception(() => AuthLog.TokenRefreshFailed(logger, new InvalidOperationException("x")));

        Assert.Null(thrown);
    }

    [Fact]
    public void A_disabled_level_writes_nothing()
    {
        var logger = new RecordingLogger { MinimumLevel = LogLevel.Error };

        AuthLog.TokenRefreshFailed(logger, new InvalidOperationException("x"));

        Assert.Empty(logger.Entries);
    }
}
