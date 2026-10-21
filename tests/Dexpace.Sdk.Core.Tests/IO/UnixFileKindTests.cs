// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.IO;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>
/// Which failures of the tar writer name a Unix socket (BODY-11, HTTP-40; design §10 entry 31). The writer opens a regular file to
/// archive it, so a failure with an errno behind it, a permission refusal or a lock, says the path is a regular file the process
/// cannot read, never a socket; the one failure that says "socket" is the managed <see cref="IOException"/> the writer throws for a
/// file type it has no tar type for.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UnixFileKindTests
{
    [Fact]
    public void The_managed_io_exception_for_an_unsupported_file_type_names_a_socket()
    {
        Assert.True(UnixFileKind.IsUnsupportedFileType(new IOException("The file type of 'p' is not supported for tar archiving.")));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(116)]
    [InlineData(11)]
    public void An_io_exception_with_an_errno_behind_it_is_not_a_socket(int errno)
    {
        Assert.False(UnixFileKind.IsUnsupportedFileType(new IOException("Input/output error : 'p'", errno)));
    }

    [Fact]
    public void A_subtype_or_another_exception_is_not_a_socket()
    {
        Assert.False(UnixFileKind.IsUnsupportedFileType(new FileNotFoundException("gone")));
        Assert.False(UnixFileKind.IsUnsupportedFileType(new DirectoryNotFoundException("gone")));
        Assert.False(UnixFileKind.IsUnsupportedFileType(new UnauthorizedAccessException("denied")));
        Assert.False(UnixFileKind.IsUnsupportedFileType(new InvalidOperationException("x")));
    }
}
