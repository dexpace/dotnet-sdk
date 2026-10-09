// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// PAGE-18, PAGE-19, PAGE-20 and PAGE-36 at the wire (phase 7c, task 3.2): the paging engine over the real
/// <see cref="SystemNetHttpClient"/> and a loopback server. These pin that the transport surfaces every <c>Link</c> line as its
/// own value, that a query-only reference resolves against the response URL, that the origin guard holds against a real second
/// server, and that the same <see cref="RequestOptions"/> instance reaches the real transport on every page.
/// </summary>
/// <remarks>
/// <para>
/// The plan's timeout row (a cursor walk whose <c>RequestOptions.Timeout</c> governs every page and throws
/// <c>ServiceRequestTimeoutException</c>) is not built: <c>SystemNetHttpClient</c> accepts <see cref="RequestOptions"/> and
/// reads none of it until phase 8b wires <c>RequestOptions.Timeout</c> (TRANSPORT-5; pinned by
/// <c>SystemNetHttpClientTests.Options_are_accepted_and_ignored_until_8b</c>), so there is no timeout to fire. The row is
/// replaced by the options-instance row below, which is PAGE-36's real claim at the seam.
/// </para>
/// <para>
/// The blocking pager does not work over this transport yet: <c>HttpResponseMessageBody</c> does not override
/// <c>ResponseBody.OpenRead</c> until phase 8b, so <c>Pageable.CreateBlocking</c> over <see cref="SystemNetHttpClient"/>, directly or
/// through a pipeline, throws <see cref="NotSupportedException"/> on the first page. The last test pins that, so 8b's change is a
/// reviewed flip of one assertion and not a silent behaviour change.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PaginationWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record WirePage(IReadOnlyList<int> Items, string? Next);

    // "1,2|next": the items, a bar, the continuation token (empty for none).
    private sealed class WireSerde : ISerde
    {
        public MediaType DefaultMediaType => MediaType.Of("text", "plain");

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            return Parse<T>(buffer.ToArray());
        }

        public void Serialize<T>(IBufferWriter<byte> destination, T value)
        {
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => Parse<T>(utf8);

        private static T? Parse<T>(ReadOnlySpan<byte> bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var bar = text.IndexOf('|', StringComparison.Ordinal);
            var items = text[..bar].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
            var next = text[(bar + 1)..];
            return (T)(object)new WirePage(items, next.Length == 0 ? null : next);
        }
    }

    // Records the options instance it was handed, then forwards to the real transport.
    private sealed class OptionsRecordingClient(IAsyncHttpClient inner) : IAsyncHttpClient
    {
        public List<RequestOptions> Seen { get; } = [];

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            Seen.Add(options);
            return inner.ExecuteAsync(request, options, cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static SystemHttpClient DirectClient() => new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    private static LoopbackResponse Page(string body, params string[] linkLines) =>
        LoopbackResponse.Status(200, "OK", linkLines.Select(line => new KeyValuePair<string, string>("Link", line)), body);

    [Fact]
    public async Task A_three_page_Link_walk_with_split_header_lines_and_a_query_only_reference()
    {
        LoopbackServer? server = null;
        server = LoopbackServer.Start(request => request.Target switch
        {
            // A query-only reference: RFC 3986 keeps the path, replaces the query.
            "/items" => Page("1|", "<?page=2>; rel=\"next\""),

            // The next link is on the second of two Link lines (PAGE-20): the transport must surface both.
            "/items?page=2" => Page("2|", $"<{server!.Url("/items?page=1")}>; rel=\"prev\"", $"<{server.Url("/items?page=3")}>; rel=\"next\""),
            "/items?page=3" => Page("3|"),
            _ => LoopbackResponse.Status(404, "Not Found"),
        });
        await using var serverScope = server;
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var pageable = Pageable.Create<WirePage, int>(
            transport,
            Request.Get(server.Url("/items").ToString()),
            new WireSerde(),
            PaginationStrategies.LinkHeader<WirePage, int>(p => p.Items),
            maxPages: 10);
        var pages = new List<Page<int>>();

        await foreach (var page in pageable.AsPages().WithCancellation(Ct))
        {
            pages.Add(page);
        }

        Assert.Equal([1, 2, 3], pages.SelectMany(p => p.Values));
        Assert.Equal(["/items", "/items?page=2", "/items?page=3"], server.Requests.Select(r => r.Target));
        Assert.Equal(2, pages[1].Headers.GetAll("Link").Count);
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_cursor_walk_hands_the_same_RequestOptions_instance_to_the_real_transport_on_every_page()
    {
        await using var server = LoopbackServer.Start(request => request.Target switch
        {
            "/items" => Page("1|a"),
            "/items?cursor=a" => Page("2|b"),
            "/items?cursor=b" => Page("3|"),
            _ => LoopbackResponse.Status(404, "Not Found"),
        });
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var recording = new OptionsRecordingClient(transport);
        var options = new RequestOptions { Timeout = TimeSpan.FromSeconds(30) }.WithTag("walk", "wire");
        var pageable = Pageable.Create<WirePage, int>(
            recording,
            Request.Get(server.Url("/items").ToString()),
            new WireSerde(),
            PaginationStrategies.Cursor<WirePage, int>(p => p.Items, p => p.Next),
            options,
            maxPages: 10);
        var items = new List<int>();

        await foreach (var item in pageable.WithCancellation(Ct))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2, 3], items);
        Assert.Equal(3, recording.Seen.Count);
        Assert.All(recording.Seen, seen => Assert.Same(options, seen));
        Assert.Equal(["/items", "/items?cursor=a", "/items?cursor=b"], server.Requests.Select(r => r.Target));
    }

    [Fact]
    public async Task A_cross_origin_Link_to_a_second_server_ends_the_walk_and_the_second_server_sees_nothing()
    {
        await using var second = LoopbackServer.Start(_ => Page("2|"));
        await using var first = LoopbackServer.Start(_ => Page("1|", $"<{second.Url("/items")}>; rel=\"next\""));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var pageable = Pageable.Create<WirePage, int>(
            transport,
            Request.Get(first.Url("/items").ToString()),
            new WireSerde(),
            PaginationStrategies.LinkHeader<WirePage, int>(p => p.Items),
            maxPages: 10);
        var items = new List<int>();

        await foreach (var item in pageable.WithCancellation(Ct))
        {
            items.Add(item);
        }

        // Same host, different port: another origin, so the walk ends after one exchange (P7c-12).
        Assert.Equal([1], items);
        Assert.Single(first.Requests);
        Assert.Empty(second.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_blocking_walk_over_the_real_transport_throws_NotSupportedException_until_8b_adds_OpenRead(bool throughPipeline)
    {
        // PIPE-28's gap below the pager, pinned as it is today: the transport's Execute works (it blocks on the async send), but
        // the body it returns has no synchronous OpenRead, and the blocking pager reads each page through OpenRead. The exchange
        // happens, the first page is requested, and the read fails with the base ResponseBody's NotSupportedException.
        //
        // When phase 8b gives HttpResponseMessageBody an OpenRead, this test fails at the Assert.Throws below. That is the
        // intended signal: replace the throw assertion with the walk (Assert.Equal([1, 2, 3], items) over the three-page cursor
        // server) and delete this comment; no 7c source changes (design "Hand-offs to later phases", 8b).
        await using var server = LoopbackServer.Start(request => request.Target switch
        {
            "/items" => Page("1|a"),
            "/items?cursor=a" => Page("2|b"),
            "/items?cursor=b" => Page("3|"),
            _ => LoopbackResponse.Status(404, "Not Found"),
        });
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);
        var pipeline = new PipelineBuilder().Build(transport);
        IHttpClient blocking = throughPipeline ? pipeline : transport;
        var pageable = Pageable.CreateBlocking<WirePage, int>(
            blocking,
            Request.Get(server.Url("/items").ToString()),
            new WireSerde(),
            PaginationStrategies.Cursor<WirePage, int>(p => p.Items, p => p.Next),
            maxPages: 10,
            cancellationToken: Ct);

        var thrown = Assert.Throws<NotSupportedException>(() =>
        {
            foreach (var item in pageable)
            {
                Assert.Fail($"No item can be read before 8b adds HttpResponseMessageBody.OpenRead, got {item}.");
            }
        });

        Assert.Contains("HttpResponseMessageBody", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("OpenRead", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(["/items"], server.Requests.Select(r => r.Target));
        Assert.Empty(server.Faults);
    }
}
