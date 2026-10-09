// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Response;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Subjects;

[Trait("Category", "Unit")]
public sealed class TransportSubjectTests
{
    [Fact]
    public void A_subject_needs_a_name()
    {
        Assert.True(Attribute.IsDefined(typeof(TransportSubject), typeof(RequiredMemberAttribute)));
        Assert.Throws<ArgumentException>(() => new TransportSubject { Name = " " });
        Assert.Throws<ArgumentNullException>(() => new TransportSubject { Name = null! });
        Assert.Equal("x", new TransportSubject { Name = "x" }.Name);
    }

    [Fact]
    public void Every_hook_defaults_to_absent_and_after_dispose_to_unspecified()
    {
        var subject = new TransportSubject { Name = "x" };

        Assert.Null(subject.CreateAsync);
        Assert.Null(subject.CreateBlocking);
        Assert.Null(subject.CreateBorrowed);
        Assert.Null(subject.CreateWithFaultingAdaptation);
        Assert.Null(subject.CreateWithInternalCancel);
        Assert.Null(subject.CreateWithNativeResend);
        Assert.Null(subject.CreateWithProxy);
        Assert.Equal(AfterDisposeBehavior.Unspecified, subject.AfterDispose);
    }

    [Fact]
    public void A_subject_with_neither_face_is_legal()
    {
        var subject = new TransportSubject { Name = "inert" };

        Assert.Null(subject.CreateAsync);
        Assert.Null(subject.CreateBlocking);
    }

    [Fact]
    public void Settings_require_a_logger()
    {
        Assert.True(Attribute.IsDefined(typeof(TransportSettings), typeof(RequiredMemberAttribute)));
        var settings = new TransportSettings { Logger = NullLogger.Instance };

        Assert.Same(NullLogger.Instance, settings.Logger);
        Assert.Equal(settings, settings with { });
    }

    [Fact]
    public async Task Internal_cancellation_exposes_what_it_was_given_and_rejects_nulls()
    {
        await using var transport = DelegateHttpClient.Create((_, _, _) => Task.FromResult<Response>(null!));
        var cancelled = 0;

        var internalCancel = new InternalCancellation(transport, () => cancelled++);
        internalCancel.CancelInFlight();

        Assert.Same(transport, internalCancel.Transport);
        Assert.Equal(1, cancelled);
        Assert.Throws<ArgumentNullException>(() => new InternalCancellation(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => new InternalCancellation(transport, null!));
    }
}
