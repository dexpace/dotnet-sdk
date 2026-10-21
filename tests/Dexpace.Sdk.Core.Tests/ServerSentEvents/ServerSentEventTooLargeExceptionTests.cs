// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The event-cap failure (SSE-19, P11-19), shaped like <see cref="ServerSentEventLineTooLongException"/>.</summary>
[Trait("Category", "Unit")]
public sealed class ServerSentEventTooLargeExceptionTests
{
    [Fact]
    public void It_is_a_sealed_StreamingException_and_so_an_SdkException()
    {
        var ex = new ServerSentEventTooLargeException(1024);

        Assert.IsAssignableFrom<StreamingException>(ex);
        Assert.IsAssignableFrom<SdkException>(ex);
        Assert.True(typeof(ServerSentEventTooLargeException).IsSealed);
    }

    [Fact]
    public void It_lives_in_the_ServerSentEvents_namespace_not_Errors() =>
        Assert.Equal("Dexpace.Sdk.Core.ServerSentEvents", typeof(ServerSentEventTooLargeException).Namespace);

    [Fact]
    public void The_three_standard_constructors_exist()
    {
        var inner = new InvalidDataException("inner");

        Assert.NotNull(new ServerSentEventTooLargeException());
        Assert.Equal("m", new ServerSentEventTooLargeException("m").Message);
        var withInner = new ServerSentEventTooLargeException("m", inner);
        Assert.Equal("m", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }

    [Fact]
    public void The_cap_constructor_names_the_cap_and_exposes_it()
    {
        var inner = new InvalidDataException("inner");

        var ex = new ServerSentEventTooLargeException(8388608, inner);

        Assert.Equal(8388608, ex.MaxEventBytes);
        Assert.Contains("8388608", ex.Message, StringComparison.Ordinal);
        Assert.Same(inner, ex.InnerException);
        Assert.Null(new ServerSentEventTooLargeException(64).InnerException);
    }

    [Fact]
    public void The_standard_constructors_leave_MaxEventBytes_zero_meaning_unknown()
    {
        Assert.Equal(0, new ServerSentEventTooLargeException().MaxEventBytes);
        Assert.Equal(0, new ServerSentEventTooLargeException("m").MaxEventBytes);
        Assert.Equal(0, new ServerSentEventTooLargeException("m", new InvalidDataException()).MaxEventBytes);
    }

    [Fact]
    public void It_is_not_retryable()
    {
        Assert.False(new ServerSentEventTooLargeException(10).IsRetryable);
    }
}
