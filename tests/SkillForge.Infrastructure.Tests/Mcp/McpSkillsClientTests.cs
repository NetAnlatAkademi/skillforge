using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;
using SkillForge.Infrastructure.Mcp;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// The skills extension's four methods, against a server shaped the way the specification's examples are.
/// </summary>
/// <remarks>
/// Transport only: what was asked, what came back, and when the walk stopped. Whether what came back is acceptable is
/// the verifier's question and is tested end to end in <see cref="McpSkillsVerificationTests"/>.
/// </remarks>
public sealed class McpSkillsClientTests
{
    [Fact]
    public async Task ListsASinglePage()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill("skill://git-workflow", "git-workflow", "Follow the team's Git conventions");
        server.AddDynamicSkill("skill://reports/daily", "daily", "Assemble today's report");

        var listing = await Client(server).ListAsync(Http());

        listing.Skills.Select(skill => skill.Uri).Should().Equal(
            "skill://git-workflow/SKILL.md",
            "skill://reports/daily/SKILL.md");
        listing.Skills[0].Manifest.Kind.Should().Be(McpSkillManifestKind.Static);
        listing.Skills[1].Manifest.Should().Be(McpSkillManifest.Dynamic);
        listing.Skills[0].TtlMs.Should().Be(300000);
        listing.Skills[0].CacheScope.Should().Be("public");
        listing.IsComplete.Should().BeTrue();
        listing.PagesRead.Should().Be(1);
    }

    [Fact]
    public async Task FollowsMultiplePagesAndOrdersByUri()
    {
        var server = new FakeSkillsServer
        {
            ListPages =
            [
                (new JsonArray(Entry("skill://zeta/SKILL.md")), "page1"),
                (new JsonArray(Entry("skill://alpha/SKILL.md")), "page2"),
                (new JsonArray(Entry("skill://mid/SKILL.md")), null),
            ],
        };

        var listing = await Client(server).ListAsync(Http());

        listing.Skills.Select(skill => skill.Uri).Should().Equal(
            "skill://alpha/SKILL.md",
            "skill://mid/SKILL.md",
            "skill://zeta/SKILL.md");
        listing.PagesRead.Should().Be(3);
        listing.IsComplete.Should().BeTrue();
        server.Requests.Count(request => request == "skills/list").Should().Be(3);
    }

    [Fact]
    public async Task AnEmptyListingIsCompleteAndEmpty()
    {
        var listing = await Client(new FakeSkillsServer()).ListAsync(Http());

        listing.Skills.Should().BeEmpty();
        listing.IsComplete.Should().BeTrue("an empty listing is an answer, which the specification allows");
    }

    [Fact]
    public async Task StopsOnARepeatedCursor()
    {
        var server = new FakeSkillsServer
        {
            ListPages =
            [
                (new JsonArray(Entry("skill://a/SKILL.md")), "page1"),
                (new JsonArray(Entry("skill://b/SKILL.md")), "page1"),
            ],
        };

        var listing = await Client(server).ListAsync(Http());

        listing.Outcome.Should().Be(McpToolPagingOutcome.CursorLoopDetected);
        listing.IsComplete.Should().BeFalse();
        listing.Skills.Should().HaveCount(2, "what was read before the repeat is true");
    }

    [Fact]
    public async Task StopsAtThePageLimit()
    {
        var server = new FakeSkillsServer { EndlessList = true };

        var listing = await Client(server).ListAsync(Http());

        listing.Outcome.Should().Be(McpToolPagingOutcome.PageLimitReached);
        listing.PagesRead.Should().Be(100);
        server.Requests.Count(request => request == "skills/list").Should().Be(100);
    }

    [Fact]
    public async Task IsolatesMalformedEntriesAndCollapsesDuplicates()
    {
        var server = new FakeSkillsServer
        {
            ListPages =
            [
                (new JsonArray(
                    Entry("skill://a/SKILL.md"),
                    new JsonObject { ["frontmatter"] = new JsonObject { ["name"] = "no-uri" } },
                    JsonValue.Create("not an object"),
                    Entry("skill://a/SKILL.md")), null),
            ],
        };

        var listing = await Client(server).ListAsync(Http());

        listing.Skills.Should().ContainSingle().Which.Uri.Should().Be("skill://a/SKILL.md");
        listing.MalformedEntries.Should().Be(2);
        listing.DuplicateEntries.Should().Be(1);
    }

    [Fact]
    public async Task GetsASkillThatWasNeverListed()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill("skill://hidden", "hidden", "Not on any listing");
        server.Listed.Clear();

        var get = await Client(server).GetAsync(Http(), "skill://hidden/SKILL.md");

        get.Value.Should().NotBeNull();
        get.Value!.Manifest.Resources.Should().ContainSingle();
        get.Value.FrontmatterJson.Should().Contain("\"name\":\"hidden\"");
        server.Requests.Should().Equal("skills/get skill://hidden/SKILL.md");
    }

    [Fact]
    public async Task ReportsAnUnservedSkillAsTheServersError()
    {
        var get = await Client(new FakeSkillsServer()).GetAsync(Http(), "skill://absent/SKILL.md");

        get.Value.Should().BeNull();
        get.Error.Should().Contain("-32602").And.Contain("No skill is served");
    }

    [Theory]
    [InlineData("{ \"x\": 1 }", "object")]
    [InlineData("\"static\"", "string")]
    [InlineData("[1, 2]", "not an object")]
    public async Task KeepsAnUnusableManifestAsInvalidRatherThanGuessing(string resources, string expected)
    {
        var server = new FakeSkillsServer();
        server.Register("skill://odd", "odd", "x", JsonNode.Parse(resources)!);

        var get = await Client(server).GetAsync(Http(), "skill://odd/SKILL.md");

        get.Value!.Manifest.Kind.Should().Be(McpSkillManifestKind.Invalid);
        get.Value.Manifest.Detail.Should().Contain(expected);
    }

    [Fact]
    public async Task ReadsTextAsUtf8AndBlobAsBase64()
    {
        var server = new FakeSkillsServer();
        server.Files["skill://a/notes.md"] = new JsonObject { ["uri"] = "skill://a/notes.md", ["text"] = "héllo" };
        server.Files["skill://a/logo.png"] = new JsonObject
        {
            ["uri"] = "skill://a/logo.png",
            ["blob"] = Convert.ToBase64String([1, 2, 3]),
        };

        var text = await Client(server).ReadAsync(Http(), "skill://a/notes.md");
        var blob = await Client(server).ReadAsync(Http(), "skill://a/logo.png");

        text.Value!.Bytes.ToArray().Should().Equal(Encoding.UTF8.GetBytes("héllo"));
        blob.Value!.Bytes.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task NeverTakesAnotherUrisContent()
    {
        var server = new FakeSkillsServer();
        server.Files["skill://a/one.md"] = new JsonObject { ["uri"] = "skill://a/other.md", ["text"] = "wrong file" };

        var read = await Client(server).ReadAsync(Http(), "skill://a/one.md");

        read.Value.Should().BeNull("verifying one file's digest against another file's bytes proves nothing");
    }

    [Fact]
    public async Task ReadsADirectoryListingAndMarksSubdirectories()
    {
        var server = new FakeSkillsServer();
        server.Directories["skill://a"] = [("skill://a/SKILL.md", false), ("skill://a/templates", true)];

        var listing = await Client(server).ReadDirectoryAsync(Http(), "skill://a");

        listing.Value!.Entries.Should().Equal(
            new McpDirectoryEntry("skill://a/SKILL.md", false),
            new McpDirectoryEntry("skill://a/templates", true));
    }

    [Fact]
    public async Task ATimeoutIsAnAnswerThatDidNotComeNotACancellation()
    {
        var server = new FakeSkillsServer { BeforeAnswer = (_, token) => Task.Delay(TimeSpan.FromSeconds(10), token) };
        var client = new Mcp20260728SkillsClient(new HttpClient(server) { Timeout = TimeSpan.FromMilliseconds(100) });

        var get = await client.GetAsync(Http(), "skill://slow/SKILL.md");

        get.Error.Should().Be("it did not answer in time");
    }

    [Fact]
    public async Task CancellationBetweenPagesStopsTheWalk()
    {
        using var cancellation = new CancellationTokenSource();
        var server = new FakeSkillsServer
        {
            EndlessList = true,
            BeforeAnswer = (_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            },
        };

        var list = () => Client(server).ListAsync(Http(), cancellation.Token);

        await list.Should().ThrowAsync<OperationCanceledException>();
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task RefusesAResponseOverTheSizeBoundRatherThanTruncatingIt()
    {
        var client = new Mcp20260728SkillsClient(new HttpClient(new OversizedHandler()));

        var read = await client.ReadAsync(Http(), "skill://big/SKILL.md");

        read.Value.Should().BeNull();
        read.Error.Should().Contain("limit");
    }

    [Fact]
    public async Task TheTimeoutCoversTheBodyNotOnlyTheHeaders()
    {
        // Headers arrive at once and the body never does: HttpClient.Timeout alone stops applying once headers are in.
        var client = new Mcp20260728SkillsClient(
            new HttpClient(new BodyHandler(new StallingStream())) { Timeout = TimeSpan.FromMilliseconds(200) });

        var read = await client.ReadAsync(Http(), "skill://slow/SKILL.md");

        read.Error.Should().Be("it did not answer in time");
    }

    [Fact]
    public async Task AConnectionResetMidBodyIsAFailedAnswerNotACrash()
    {
        var client = new Mcp20260728SkillsClient(new HttpClient(new BodyHandler(new ResettingStream())));

        var read = await client.ReadAsync(Http(), "skill://reset/SKILL.md");

        read.Value.Should().BeNull();
        read.Error.Should().Contain("reset");
    }

    [Fact]
    public async Task NamesNoUrlWithoutSendingAnything()
    {
        var server = new FakeSkillsServer();
        var stdio = new McpServerDeclaration("local", "file", McpTransport.Stdio, null, [], [], "/tmp/mcp.json");

        var listing = await Client(server).ListAsync(stdio);

        listing.Detail.Should().Be("the declaration names no URL");
        server.Requests.Should().BeEmpty();
    }

    internal static McpServerDeclaration Http() =>
        new("skills", "file", McpTransport.Http, FakeSkillsServer.Url, [], [], "/tmp/mcp.json");

    private static Mcp20260728SkillsClient Client(HttpMessageHandler handler) => new(new HttpClient(handler));

    private static JsonObject Entry(string uri) =>
        new()
        {
            ["uri"] = uri,
            ["frontmatter"] = new JsonObject { ["name"] = "x", ["description"] = "x" },
            ["resources"] = "dynamic",
        };

    private sealed class BodyHandler(Stream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
    }

    /// <summary>A body that never sends a byte, and only stops when cancelled.</summary>
    private sealed class StallingStream : EndlessStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    /// <summary>A body whose connection drops after the headers.</summary>
    private sealed class ResettingStream : EndlessStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("connection reset by peer");
    }

    /// <summary>A server that streams more than the bound allows, without declaring a length.</summary>
    private sealed class OversizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new EndlessStream()),
            });
    }

    private class EndlessStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)' ', offset, count);
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
