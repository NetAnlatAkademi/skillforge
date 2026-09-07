using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure.Mcp;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// The <c>tools/list</c> walk, against handlers that page the way the specification's pagination does.
/// </summary>
/// <remarks>
/// Every case here is about a number a report will print. A walk that stops early produces a count that is a
/// floor, and the difference between a floor and a total is the whole value of a declared-versus-runtime
/// comparison — so "how many pages were read and why did it stop" is asserted as carefully as the tools.
/// </remarks>
public sealed class McpToolPaginationTests
{
    private const string Url = "https://mcp.example.test/mcp";

    [Fact]
    public async Task ReadsASingleUnpagedResponse()
    {
        var probe = await Probe(Pages([Tools("alpha", "beta")]));

        probe.ToolsOrEmpty.Select(tool => tool.Name).Should().Equal("alpha", "beta");
        probe.Paging.Should().Be(new McpToolPaging(1, 2, McpToolPagingOutcome.Complete));
    }

    [Fact]
    public async Task FollowsTwoPages()
    {
        var handler = Pages([Tools("alpha") with { Next = "c1" }, Tools("beta")]);

        var probe = await Probe(handler);

        probe.ToolsOrEmpty.Select(tool => tool.Name).Should().Equal("alpha", "beta");
        probe.Paging!.PagesRead.Should().Be(2);
        probe.Paging.IsComplete.Should().BeTrue();

        // The cursor goes where the specification's pagination puts it, and is absent on the first request rather
        // than sent as null: a server is entitled to reject an unknown empty cursor.
        // Bodies[0] is server/discover; the tool pages follow it.
        handler.Bodies[1].Should().NotContain("cursor");
        handler.Bodies[2].Should().Contain("\"cursor\":\"c1\"");
    }

    [Fact]
    public async Task FollowsThreePagesAndCountsEveryToolOnce()
    {
        // The document's own example: 50, 50, 7.
        var handler = Pages(
        [
            Numbered(0, 50) with { Next = "c1" },
            Numbered(50, 50) with { Next = "c2" },
            Numbered(100, 7),
        ]);

        var probe = await Probe(handler);

        probe.ToolsOrEmpty.Should().HaveCount(107);
        probe.Paging.Should().Be(new McpToolPaging(3, 50, McpToolPagingOutcome.Complete));
    }

    [Fact]
    public async Task TreatsAnEmptyNextCursorAsTheEndOfTheList()
    {
        // A server that sends "nextCursor": "" has ended the list. Asking for page after page of nothing would be
        // a loop that never trips the loop guard, because the cursor never repeats — it is always absent.
        var handler = Pages([Tools("alpha") with { Next = string.Empty }]);

        var probe = await Probe(handler);

        probe.Paging.Should().Be(new McpToolPaging(1, 1, McpToolPagingOutcome.Complete));
        handler.Bodies.Should().HaveCount(2, "one server/discover and one tools/list");
    }

    [Fact]
    public async Task KeepsOneEntryPerNameWhenAServerRepeatsATool()
    {
        var handler = Pages([Tools("alpha", "beta") with { Next = "c1" }, Tools("beta", "gamma")]);

        var probe = await Probe(handler);

        // Three names, not four: a name listed twice is one tool whose schema is ambiguous, and the count a
        // surface report prints must not double it.
        probe.ToolsOrEmpty.Select(tool => tool.Name).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task DistinguishesNamesThatDifferOnlyInCase()
    {
        // The specification identifies a tool by its name and does not say two names differing in case are the
        // same tool. Folding case here would silently merge two real tools.
        var probe = await Probe(Pages([Tools("Deploy", "deploy")]));

        probe.ToolsOrEmpty.Select(tool => tool.Name).Should().Equal("Deploy", "deploy");
    }

    [Fact]
    public async Task StopsWhenAServerHandsBackACursorItAlreadyGave()
    {
        var handler = Pages(
        [
            Tools("alpha") with { Next = "same" },
            Tools("beta") with { Next = "same" },
        ]);

        var probe = await Probe(handler);

        probe.Paging!.Outcome.Should().Be(McpToolPagingOutcome.CursorLoopDetected);
        probe.Paging.IsComplete.Should().BeFalse();

        // What was read is kept, because it is true.
        probe.ToolsOrEmpty.Select(tool => tool.Name).Should().Equal("alpha", "beta");
    }

    [Fact]
    public async Task StopsAtTheHundredthPageWhenAServerNeverEnds()
    {
        var handler = new EndlessHandler();

        var probe = await Adapter(handler).ProbeAsync(Http());

        probe.Paging!.PagesRead.Should().Be(100);
        probe.Paging.Outcome.Should().Be(McpToolPagingOutcome.PageLimitReached);
        probe.ToolsOrEmpty.Should().HaveCount(100, "one distinct tool per page");

        // The bound is on requests, not only on the reported number: an unbounded walk is the failure this guard
        // exists to prevent, so the count of calls is what has to be asserted.
        handler.ToolCalls.Should().Be(100);
    }

    [Fact]
    public async Task CancelsBetweenPagesRatherThanOnlyAtTheStart()
    {
        using var cancellation = new CancellationTokenSource();

        // Cancelled once the first page is in hand, which is the case a token checked only on entry would miss.
        var handler = new EndlessHandler(onToolCall: _ => cancellation.Cancel());

        var probe = () => Adapter(handler).ProbeAsync(Http(), cancellation.Token);

        await probe.Should().ThrowAsync<OperationCanceledException>();
        handler.ToolCalls.Should().Be(1);
    }

    [Fact]
    public async Task KeepsThePagesItReadWhenTheServerStopsAnswering()
    {
        var handler = Pages([Tools("alpha") with { Next = "c1" }], failFrom: 2);

        var probe = await Probe(handler);

        probe.ToolsOrEmpty.Select(tool => tool.Name).Should().Equal("alpha");
        probe.Paging!.IsComplete.Should().BeFalse("less was read than the server has");
    }

    [Fact]
    public async Task ReportsAnEmptyListWithoutClaimingItWasCutShort()
    {
        // A first page that fails is a server refusing its tool list. Nothing was read, so nothing is incomplete —
        // an empty list is the honest result, and the probe itself already succeeded.
        var handler = Pages([], failFrom: 1);

        var probe = await Probe(handler);

        probe.ToolsOrEmpty.Should().BeEmpty();
        probe.Paging.Should().Be(new McpToolPaging(0, 0, McpToolPagingOutcome.Complete));
    }

    [Fact]
    public async Task NeverAsksForToolsFromAServerThatDeclaresNone()
    {
        var handler = new PagingHandler([], 0, capabilities: "resources");

        var probe = await Adapter(handler).ProbeAsync(Http());

        probe.Status.Should().Be(McpProbeStatus.Answered);
        probe.Paging.Should().BeNull("no list was asked for, which is not the same as an empty one");
        handler.Bodies.Should().HaveCount(1);
    }

    private static async Task<McpServerProbe> Probe(PagingHandler handler) =>
        await Adapter(handler).ProbeAsync(Http());

    private static Mcp20260728ProtocolAdapter Adapter(HttpMessageHandler handler) => new(new HttpClient(handler));

    private static McpServerDeclaration Http() =>
        new("remote", "claude-code", McpTransport.Http, Url, [], [], "/home/dev/.claude.json");

    private static PagingHandler Pages(IReadOnlyList<Page> pages, int failFrom = 0) => new(pages, failFrom);

    private static Page Tools(params string[] names) => new(names, null);

    private static Page Numbered(int from, int count) =>
        new([.. Enumerable.Range(from, count).Select(index => $"tool_{index:D3}")], null);

    private sealed record Page(IReadOnlyList<string> Names, string? Next);

    /// <summary>
    /// Answers <c>server/discover</c> once, then hands out the given pages in order.
    /// </summary>
    private sealed class PagingHandler(IReadOnlyList<Page> pages, int failFrom, string capabilities = "tools")
        : HttpMessageHandler
    {
        private int _toolCalls;

        internal List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);

            if (body.Contains("server/discover", StringComparison.Ordinal))
            {
                return Json($$"""
                    {
                      "jsonrpc": "2.0",
                      "id": "1",
                      "result": {
                        "supportedVersions": ["2026-07-28"],
                        "capabilities": { "{{capabilities}}": {} }
                      }
                    }
                    """);
            }

            _toolCalls++;

            if (failFrom > 0 && _toolCalls >= failFrom)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("nope", Encoding.UTF8, "application/json"),
                };
            }

            var page = pages[_toolCalls - 1];

            var result = new JsonObject
            {
                ["tools"] = new JsonArray([.. page.Names.Select(name => (JsonNode)new JsonObject
                {
                    ["name"] = name,
                    ["inputSchema"] = new JsonObject { ["type"] = "object" },
                })]),
            };

            if (page.Next is not null)
            {
                result["nextCursor"] = page.Next;
            }

            return Json(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = "1",
                ["result"] = result,
            }.ToJsonString());
        }

        private static HttpResponseMessage Json(string payload) =>
            new(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
    }

    /// <summary>A server that always has one more page — the shape the page guard exists for.</summary>
    private sealed class EndlessHandler(Action<int>? onToolCall = null) : HttpMessageHandler
    {
        private int _toolCalls;

        internal int ToolCalls => _toolCalls;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(CancellationToken.None);

            if (body.Contains("server/discover", StringComparison.Ordinal))
            {
                return Json("""
                    {
                      "jsonrpc": "2.0",
                      "id": "1",
                      "result": {
                        "supportedVersions": ["2026-07-28"],
                        "capabilities": { "tools": {} }
                      }
                    }
                    """);
            }

            var page = ++_toolCalls;
            onToolCall?.Invoke(page);

            return Json($$"""
                {
                  "jsonrpc": "2.0",
                  "id": "1",
                  "result": {
                    "tools": [{ "name": "tool_{{page}}", "inputSchema": { "type": "object" } }],
                    "nextCursor": "cursor-{{page}}"
                  }
                }
                """);
        }

        private static HttpResponseMessage Json(string payload) =>
            new(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
    }
}
