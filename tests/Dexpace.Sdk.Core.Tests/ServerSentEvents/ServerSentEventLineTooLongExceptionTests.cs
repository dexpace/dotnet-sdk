// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The cap failure (SSE-19, P7b-4, P7b-20).</summary>
[Trait("Category", "Unit")]
public class ServerSentEventLineTooLongExceptionTests
{
    [Fact]
    public void It_is_a_StreamingException_and_so_an_SdkException()
    {
        var ex = new ServerSentEventLineTooLongException(1024);

        Assert.IsAssignableFrom<StreamingException>(ex);
        Assert.IsAssignableFrom<SdkException>(ex);
    }

    [Fact]
    public void It_lives_in_the_ServerSentEvents_namespace_not_Errors() =>
        Assert.Equal("Dexpace.Sdk.Core.ServerSentEvents", typeof(ServerSentEventLineTooLongException).Namespace);

    [Fact]
    public void The_three_standard_constructors_exist()
    {
        var inner = new InvalidDataException("inner");

        Assert.NotNull(new ServerSentEventLineTooLongException());
        Assert.Equal("m", new ServerSentEventLineTooLongException("m").Message);
        var withInner = new ServerSentEventLineTooLongException("m", inner);
        Assert.Equal("m", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }

    [Fact]
    public void The_cap_constructor_names_the_cap_and_exposes_it()
    {
        var inner = new InvalidDataException("inner");

        var ex = new ServerSentEventLineTooLongException(1048576, inner);

        Assert.Equal(1048576, ex.MaxLineBytes);
        Assert.Contains("1048576", ex.Message, StringComparison.Ordinal);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void The_cap_constructor_takes_an_optional_inner_exception()
    {
        var ex = new ServerSentEventLineTooLongException(64);

        Assert.Equal(64, ex.MaxLineBytes);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void The_standard_constructors_leave_MaxLineBytes_zero_meaning_unknown()
    {
        Assert.Equal(0, new ServerSentEventLineTooLongException().MaxLineBytes);
        Assert.Equal(0, new ServerSentEventLineTooLongException("m").MaxLineBytes);
        Assert.Equal(0, new ServerSentEventLineTooLongException("m", new InvalidDataException()).MaxLineBytes);
    }

    [Fact]
    public void It_is_not_retryable()
    {
        Assert.False(new ServerSentEventLineTooLongException(10).IsRetryable);
    }
}
