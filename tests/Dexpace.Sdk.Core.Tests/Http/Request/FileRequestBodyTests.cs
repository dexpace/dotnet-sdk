// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/body-file/src/file-body.test.ts: recognisable and replayable; missing path and
// directory rejected; negative start and out-of-range count rejected; the destination is not closed; the exact range is
// written; count 0; a fresh handle per write; read and write errors propagate. Added here: the symlink size (design fact 3),
// the shrink (fact 4), and count 0 never opening the file (P3b-5).

#pragma warning disable CA2000 // The streams and bodies under test are owned by the test.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="FileRequestBody"/>: HTTP-40, BODY-8 (file half), BODY-11, BODY-12, BODY-13; consumer of HTTP-39/BODY-10.</summary>
[Trait("Category", "Unit")]
public sealed class FileRequestBodyTests : IDisposable
{
    private static readonly byte[] s_content = Encoding.ASCII.GetBytes("0123456789abcdef");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dexpace-filebody-" + Guid.NewGuid().ToString("N"));

    public FileRequestBodyTests() => Directory.CreateDirectory(_directory);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string NewFile(byte[]? content = null, string name = "data.bin")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content ?? s_content);
        return path;
    }

    private static async Task<byte[]> WriteAsync(RequestBody body)
    {
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, Token);
        return sink.ToArray();
    }

    private static byte[] WriteSync(RequestBody body)
    {
        using var sink = new MemoryStream();
        body.WriteTo(sink, Token);
        return sink.ToArray();
    }

    [Fact]
    public void Is_recognisable_by_type_and_exposes_its_range()
    {
        var path = NewFile();

        var body = RequestBody.FromFile(path, offset: 4, count: 6);

        Assert.IsType<FileRequestBody>(body);
        Assert.Equal(Path.GetFullPath(path), body.FilePath);
        Assert.Equal(4, body.Offset);
        Assert.Equal(6, body.ContentLength);
    }

    [Fact]
    public async Task Is_replayable_and_two_writes_are_byte_identical()
    {
        var body = RequestBody.FromFile(NewFile());

        Assert.True(body.IsReplayable);
        Assert.Same(body, await body.ToReplayableAsync(Token));
        Assert.Equal(s_content, await WriteAsync(body));
        Assert.Equal(s_content, await WriteAsync(body));
        Assert.Equal(s_content, WriteSync(body));
        Assert.Equal(s_content.Length, body.ContentLength);
    }

    [Fact]
    public void A_missing_path_throws_FileNotFoundException() =>
        Assert.Throws<FileNotFoundException>(() => RequestBody.FromFile(Path.Combine(_directory, "missing.bin")));

    [Fact]
    public void A_directory_throws_ArgumentException() =>
        Assert.Throws<ArgumentException>(() => RequestBody.FromFile(_directory));

    [Fact]
    public void An_empty_path_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RequestBody.FromFile(string.Empty));
        Assert.Throws<ArgumentNullException>(() => RequestBody.FromFile(null!));
    }

    [Fact]
    public void A_negative_offset_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestBody.FromFile(NewFile(), offset: -1));

    [Theory]
    [InlineData(17, -1)]
    [InlineData(0, 17)]
    [InlineData(10, 7)]
    [InlineData(0, -2)]
    public void An_offset_or_count_past_the_size_is_rejected(long offset, long count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestBody.FromFile(NewFile(), offset: offset, count: count));

    [Fact]
    public async Task The_destination_is_not_closed()
    {
        var body = RequestBody.FromFile(NewFile());
        var sink = new DisposeCountingStream(new MemoryStream());

        await body.WriteToAsync(sink, Token);
        body.WriteTo(sink, Token);

        Assert.Equal(0, sink.DisposeCount);
        Assert.True(sink.CanWrite);
    }

    [Fact]
    public async Task The_exact_range_is_written()
    {
        var body = RequestBody.FromFile(NewFile(), offset: 4, count: 6);

        Assert.Equal(Encoding.ASCII.GetBytes("456789"), await WriteAsync(body));
        Assert.Equal(Encoding.ASCII.GetBytes("456789"), WriteSync(body));
    }

    [Fact]
    public async Task Count_minus_one_means_the_rest_of_the_file()
    {
        var body = RequestBody.FromFile(NewFile(), offset: 10);

        Assert.Equal(6, body.ContentLength);
        Assert.Equal(Encoding.ASCII.GetBytes("abcdef"), await WriteAsync(body));
    }

    [Fact]
    public async Task Count_zero_writes_nothing_and_succeeds()
    {
        var body = RequestBody.FromFile(NewFile(), offset: 3, count: 0);

        Assert.Equal(0, body.ContentLength);
        Assert.Empty(await WriteAsync(body));
        Assert.Empty(WriteSync(body));
    }

    [Fact]
    public async Task Count_zero_on_a_zero_size_file_never_opens_it()
    {
        var path = NewFile([]);
        var body = RequestBody.FromFile(path);

        // P3b-5: a zero count never opens the file, so a file that has gone missing does not matter.
        File.Delete(path);

        Assert.Equal(0, body.ContentLength);
        Assert.Empty(await WriteAsync(body));
        Assert.Empty(WriteSync(body));
    }

    [Fact]
    public async Task A_fresh_handle_is_opened_per_write()
    {
        var path = NewFile();
        var body = RequestBody.FromFile(path);

        _ = await WriteAsync(body);
        // A write holds no handle after it returns, so the file can be deleted between writes on any platform.
        File.Delete(path);
        File.WriteAllBytes(path, s_content);

        Assert.Equal(s_content, await WriteAsync(body));
    }

    [Fact]
    public async Task A_read_error_propagates()
    {
        var path = NewFile();
        var body = RequestBody.FromFile(path);
        File.Delete(path);

        await Assert.ThrowsAsync<FileNotFoundException>(() => WriteAsync(body));
        Assert.Throws<FileNotFoundException>(() => WriteSync(body));
    }

    [Fact]
    public async Task A_write_error_propagates()
    {
        var body = RequestBody.FromFile(NewFile());
        using var asyncSink = new FailingWriteStream(1, new IOException("sink failed"));
        using var syncSink = new FailingWriteStream(1, new IOException("sink failed"));

        await Assert.ThrowsAsync<IOException>(() => body.WriteToAsync(asyncSink, Token));
        Assert.Throws<IOException>(() => body.WriteTo(syncSink, Token));
    }

    [Fact]
    public async Task A_symbolic_link_is_resolved_and_its_target_size_is_used()
    {
        var target = NewFile(Encoding.ASCII.GetBytes("target"), "reg.txt");
        var link = Path.Combine(_directory, "link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // This OS cannot create links here (for example a Windows account without the privilege): nothing to prove.
            return;
        }

        var body = RequestBody.FromFile(link);

        Assert.Equal(6, body.ContentLength);
        Assert.Equal(Encoding.ASCII.GetBytes("target"), await WriteAsync(body));
    }

    [Fact]
    public async Task A_file_that_shrinks_after_construction_fails_naming_transferred_of_total()
    {
        var path = NewFile(new byte[100]);
        var body = RequestBody.FromFile(path);
        File.WriteAllBytes(path, new byte[10]);

        var async = await Assert.ThrowsAsync<EndOfStreamException>(() => WriteAsync(body));
        var sync = Assert.Throws<EndOfStreamException>(() => WriteSync(body));

        Assert.Contains("10 of 100", async.Message, StringComparison.Ordinal);
        Assert.Contains("10 of 100", sync.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_handle_is_released_after_each_write_and_after_a_failed_write()
    {
        var path = NewFile();
        var body = RequestBody.FromFile(path);
        using var failing = new FailingWriteStream(1, new IOException("sink failed"));

        _ = await WriteAsync(body);
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        await Assert.ThrowsAsync<IOException>(() => body.WriteToAsync(failing, Token));
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }

    [Fact]
    public async Task Growth_past_the_count_is_ignored()
    {
        var path = NewFile();
        var body = RequestBody.FromFile(path);
        File.AppendAllText(path, "extra");

        Assert.Equal(s_content, await WriteAsync(body));
    }

    [Fact]
    public async Task The_file_may_be_open_for_writing_elsewhere()
    {
        var path = NewFile();
        var body = RequestBody.FromFile(path);

        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        Assert.Equal(s_content, await WriteAsync(body));
    }

    [Fact]
    public void The_content_type_is_never_guessed()
    {
        Assert.Null(RequestBody.FromFile(NewFile(name: "report.json")).ContentType);
        Assert.Equal(CommonMediaTypes.TextPlain, RequestBody.FromFile(NewFile(), CommonMediaTypes.TextPlain).ContentType);
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_write_before_a_handle_is_opened()
    {
        var body = RequestBody.FromFile(NewFile());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => body.WriteToAsync(new MemoryStream(), cts.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => body.WriteTo(new MemoryStream(), cts.Token));
    }

    [Fact]
    public void Equality_is_identity()
    {
        var path = NewFile();
        var first = RequestBody.FromFile(path);
        var second = RequestBody.FromFile(path);

        Assert.Equal(first, first);
        Assert.NotEqual<RequestBody>(first, second);
    }
}
