// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.IO;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>
/// IO-2, mechanised once: core never issues a zero-count read (P3a-7). Every helper and body method runs over a
/// <see cref="StrictReadStream"/> across source lengths 0, 1, a chunk, and a chunk plus one, and none may request zero.
/// Each earlier test asserts its own slice; this is the union, so a later helper that forgets the rule fails here.
/// </summary>
[Trait("Category", "Unit")]
public class ZeroCountReadRuleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<int> SourceLengths => [0, 1, 7, StreamCopy.ChunkSize, StreamCopy.ChunkSize + 1];

    private static StrictReadStream Source(int length) =>
        new(new ChunkedReadStream(new byte[length], 40_000));

    private static async Task Tolerate(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is BodyTooLargeException or EndOfStreamException)
        {
            // A refusal is a legitimate outcome here; the rule under test is about the reads issued on the way.
        }
    }

    private static void AssertNoZeroCount(StrictReadStream source) =>
        Assert.All(source.RequestedCounts, count => Assert.True(count >= 1, $"a read requested {count} bytes."));

    [Theory]
    [MemberData(nameof(SourceLengths))]
    public async Task StreamCopy_issues_no_zero_count_read_in_any_operation(int length)
    {
        foreach (var useAsync in new[] { false, true })
        {
            using var destination = new MemoryStream();

            using (var s = Source(length))
            {
                _ = useAsync ? await StreamCopy.CopyToEndAsync(s, destination, Token) : StreamCopy.CopyToEnd(s, destination, Token);
                AssertNoZeroCount(s);
            }

            using (var s = Source(length))
            {
                var count = Math.Min(length, 10);
                if (useAsync)
                {
                    await StreamCopy.CopyExactlyAsync(s, destination, count, Token);
                }
                else
                {
                    StreamCopy.CopyExactly(s, destination, count, Token);
                }

                AssertNoZeroCount(s);
            }

            using (var s = Source(length))
            {
                var into = new ArrayBufferWriter<byte>();
                _ = useAsync
                    ? await StreamCopy.DrainUpToAsync(s, into, 10, Token)
                    : StreamCopy.DrainUpTo(s, into, 10, Token);
                AssertNoZeroCount(s);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SourceLengths))]
    public async Task Utf8LineReader_issues_no_zero_count_read_in_either_mode_or_form(int length)
    {
        foreach (var mode in new[] { LineTerminators.LineFeedOrCrLf, LineTerminators.Whatwg })
        {
            foreach (var useAsync in new[] { false, true })
            {
                using var source = new StrictReadStream(new ChunkedReadStream(Enumerable.Repeat((byte)'\n', length).ToArray(), 5000));
                using var reader = new Utf8LineReader(source, mode);

                while ((useAsync ? await reader.ReadLineAsync(Token) : reader.ReadLine()) is not null)
                {
                }

                AssertNoZeroCount(source);
            }
        }
    }

    [Theory]
    [MemberData(nameof(SourceLengths))]
    public async Task BodyMaterializer_issues_no_zero_count_read(int length)
    {
        foreach (var useAsync in new[] { false, true })
        {
            using var source = Source(length);
            await Tolerate(async () => _ = useAsync
                ? await BodyMaterializer.ReadAllAsync(source, -1, 100, Token)
                : BodyMaterializer.ReadAll(source, -1, 100, Token));
            AssertNoZeroCount(source);
        }
    }

    [Theory]
    [MemberData(nameof(SourceLengths))]
    public async Task RequestBody_ToReplayable_issues_no_zero_count_read(int length)
    {
        foreach (var useAsync in new[] { false, true })
        {
            using var source = Source(length);
            var body = RequestBody.FromStream(source, contentLength: length);
            _ = useAsync ? await body.ToReplayableAsync(Token) : body.ToReplayable(Token);
            AssertNoZeroCount(source);
        }
    }

    [Theory]
    [MemberData(nameof(SourceLengths))]
    public async Task ResponseBody_readers_issue_no_zero_count_read(int length)
    {
        foreach (var useAsync in new[] { false, true })
        {
            using var bytesSource = Source(length);
            await Tolerate(async () => _ = useAsync
                ? await ResponseBody.FromStream(bytesSource).ReadAsBytesAsync(Token)
                : ResponseBody.FromStream(bytesSource).ReadAsBytes(Token));
            AssertNoZeroCount(bytesSource);

            using var stringSource = Source(length);
            await Tolerate(async () => _ = useAsync
                ? await ResponseBody.FromStream(stringSource).ReadAsStringAsync(Token)
                : ResponseBody.FromStream(stringSource).ReadAsString(Token));
            AssertNoZeroCount(stringSource);
        }
    }
}
