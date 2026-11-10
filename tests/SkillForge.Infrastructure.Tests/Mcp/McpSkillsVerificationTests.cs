using System.Text;
using System.Text.Json.Nodes;
using SkillForge.Application.Mcp;
using SkillForge.Application.Migration;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Infrastructure.Mcp;
using SkillForge.Infrastructure.Migration;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// <c>mcp skills verify</c> end to end: a configuration file on disk, the real readers, the real prober, the real
/// frontmatter parser and SHA-256 — and an in-memory server, so nothing leaves the machine.
/// </summary>
/// <remarks>
/// One test per case the brief named. Each asserts the status and the exact codes, because "it produced some
/// findings" is how a check that fires on the wrong thing passes review.
/// </remarks>
public sealed class McpSkillsVerificationTests : IDisposable
{
    private const string Root = "skill://pdf-processing";
    private const string SkillUri = Root + "/SKILL.md";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "skillforge-mcp-skills-tests",
        Guid.NewGuid().ToString("n"));

    public McpSkillsVerificationTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task AStaticSkillWhoseBytesMatchIsAnIntegrityMatch()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs", ("templates/invoice.md", "# Invoice\n"));

        var (verification, report) = await VerifyOne(server);

        verification.Status.Should().Be(McpSkillIntegrityStatus.IntegrityMatch);
        verification.Resources.Should().HaveCount(2).And.OnlyContain(check => check.Matches);
        verification.Name.Should().Be("pdf-processing");
        verification.TtlMs.Should().Be(300000);
        verification.CacheScope.Should().Be("public");
        report.Diagnostics.Should().BeEmpty();
        report.HasIntegrityFindings.Should().BeFalse();
    }

    [Fact]
    public async Task RecordsProvenanceWithoutClaimingTheServerIsVerified()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");

        var (verification, _) = await VerifyOne(server);

        var provenance = verification.Provenance!;
        provenance.Endpoint.Should().Be(FakeSkillsServer.Url);
        provenance.SelfReportedName.Should().Be("fake-skills");
        provenance.SelfReportedVersion.Should().Be("1.2.3");
        provenance.ProtocolVersion.Should().Be("2026-07-28");
        provenance.SkillUri.Should().Be(SkillUri);
        provenance.Manifest.Should().ContainSingle().Which.Digest.Should().StartWith("sha256:");
        McpSkillProvenance.Origin.Should().Be("McpServer");
    }

    [Fact]
    public async Task ChangedBytesUnderTheSameSizeAreADigestMismatch()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs", ("notes.md", "AAAA"));
        server.Files[$"{Root}/notes.md"]["text"] = "BBBB";

        var (verification, report) = await VerifyOne(server);

        verification.Status.Should().Be(McpSkillIntegrityStatus.IntegrityMismatch);
        Codes(report).Should().Equal(DiagnosticCodes.McpSkillDigestMismatch);
    }

    [Fact]
    public async Task ADifferentLengthIsReportedOnceAsASizeMismatch()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs", ("notes.md", "AAAA"));
        server.Files[$"{Root}/notes.md"]["text"] = "AAAA plus a payload";

        var (_, report) = await VerifyOne(server);

        Codes(report).Should().Equal(
            [DiagnosticCodes.McpSkillSizeMismatch],
            "one changed file is one finding, not a size finding and a digest finding");
    }

    [Fact]
    public async Task AFrontmatterTheSkillFileDoesNotContainIsDrift()
    {
        var server = new FakeSkillsServer();
        var skill = server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");
        skill["frontmatter"]!["description"] = "Something reassuring";

        var (verification, report) = await VerifyOne(server);

        verification.Status.Should().Be(McpSkillIntegrityStatus.IntegrityMismatch);
        verification.FrontmatterDrift.Should().ContainSingle()
            .Which.Should().Be("description: 'Something reassuring' · 'Extract PDFs'");
        Codes(report).Should().Equal(DiagnosticCodes.McpSkillFrontmatterDrift);
    }

    [Fact]
    public async Task AFieldOnlyTheSkillFileDeclaresIsAlsoDrift()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");
        var text = FakeSkillsServer.SkillFile("pdf-processing", "Extract PDFs", "allowed-tools:\n  - Bash\n");
        Reserve(server, $"{Root}/SKILL.md", text);

        var (verification, _) = await VerifyOne(server);

        verification.FrontmatterDrift.Should().ContainSingle().Which.Should().StartWith("allowed-tools: [] · [Bash]");
    }

    [Fact]
    public async Task AFileInTheDirectoryButNotInTheManifestIsUnlistedAndNeverRead()
    {
        var server = new FakeSkillsServer { DirectoryRead = true };
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");
        server.Directories[Root] = [($"{Root}/SKILL.md", false), ($"{Root}/scripts", true)];
        server.Directories[$"{Root}/scripts"] = [($"{Root}/scripts/install.sh", false)];

        var (verification, report) = await VerifyOne(server);

        verification.UnlistedResources.Should().Equal($"{Root}/scripts/install.sh");
        verification.DirectoryEnumeration.Should().Be("complete");
        Codes(report).Should().Equal(DiagnosticCodes.McpSkillUnlistedResource);
        server.Requests.Should().NotContain($"resources/read {Root}/scripts/install.sh");
    }

    [Fact]
    public async Task ADirectoryListingNeverWidensWhatIsLookedAt()
    {
        var server = new FakeSkillsServer { DirectoryRead = true };
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");
        server.Directories[Root] =
        [
            ($"{Root}/SKILL.md", false),
            ("skill://other/SKILL.md", false),
            ($"{Root}/../escape", true),
            ("file:///etc/passwd", false),
        ];

        var (verification, report) = await VerifyOne(server);

        verification.UnlistedResources.Should().BeEmpty();
        verification.DirectoryEnumeration.Should().Be(
            "partial",
            "something the server listed was not looked at, so 'no unlisted files' is not a claim the report can make");
        report.Diagnostics.Should().BeEmpty();
        server.Requests.Should().NotContain(request => request.Contains("escape", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithoutDirectoryReadTheMethodIsNeverCalled()
    {
        var server = new FakeSkillsServer { DirectoryRead = false };
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");

        var (verification, _) = await VerifyOne(server);

        verification.DirectoryEnumeration.Should().Be("not declared");
        server.Requests.Should().NotContain(request => request.StartsWith("resources/directory/read", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AManifestFileThatCannotBeReadIsMissing()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs", ("gone.md", "x"));
        server.Files.Remove($"{Root}/gone.md");

        var (verification, report) = await VerifyOne(server);

        verification.Status.Should().Be(McpSkillIntegrityStatus.IntegrityMismatch);
        verification.Resources.Single(check => check.Uri.EndsWith("gone.md", StringComparison.Ordinal))
            .Detail.Should().Contain("-32602");
        Codes(report).Should().Equal(DiagnosticCodes.McpSkillMissingResource);
    }

    [Fact]
    public async Task ADynamicSkillIsNotAFaultAndHasNoFabricatedBaseline()
    {
        var server = new FakeSkillsServer();
        server.AddDynamicSkill("skill://reports/daily", "daily", "Today's report");

        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: true, []);

        var verification = report.Servers.Single().Verifications.Single();
        verification.Status.Should().Be(McpSkillIntegrityStatus.NotEstablished);
        verification.Resources.Should().BeEmpty();
        verification.Provenance!.Manifest.Should().BeEmpty();
        report.Diagnostics.Should().ContainSingle().Which.Should().Match<Diagnostic>(finding =>
            finding.Code == DiagnosticCodes.McpSkillDynamicIntegrityLimit
            && finding.Severity == DiagnosticSeverity.Info
            && finding.Message.Contains("Static content integrity cannot be established", StringComparison.Ordinal));
        report.HasIntegrityFindings.Should().BeFalse();
        server.Requests.Should().NotContain(request => request.StartsWith("resources/read", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("sha256:ABCDEF", "64 lowercase hexadecimal")]
    [InlineData("sha512:00", "other than sha256")]
    [InlineData(null, "declares no digest")]
    public async Task AnUnusableDigestMakesTheManifestInvalidAndNothingIsRead(string? digest, string expected)
    {
        var server = new FakeSkillsServer();
        server.Register(Root, "pdf-processing", "Extract PDFs", new JsonArray(new JsonObject
        {
            ["uri"] = SkillUri,
            ["digest"] = digest,
            ["size"] = 10,
        }));

        var (verification, report) = await VerifyOne(server);

        verification.Status.Should().Be(McpSkillIntegrityStatus.InvalidManifest);
        verification.Detail.Should().Contain(expected);
        Codes(report).Should().Equal(DiagnosticCodes.McpSkillInvalidManifest);
        server.Requests.Should().NotContain(request => request.StartsWith("resources/read", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AManifestOverTheSpecificationsSizeLimitIsInvalid()
    {
        var server = new FakeSkillsServer();
        server.Register(Root, "pdf-processing", "Extract PDFs", new JsonArray(new JsonObject
        {
            ["uri"] = SkillUri,
            ["digest"] = "sha256:" + new string('0', 64),
            ["size"] = McpSkillManifestRules.MaxTotalBytes + 1,
        }));

        var (verification, _) = await VerifyOne(server);

        verification.Status.Should().Be(McpSkillIntegrityStatus.InvalidManifest);
        verification.Detail.Should().Contain("over the specification's limit");
    }

    [Fact]
    public async Task ASkillCanBeVerifiedByUriWithoutBeingListed()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");
        server.Listed.Clear();

        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: true, [SkillUri]);

        var verification = report.Servers.Single().Verifications.Single();
        verification.Listed.Should().BeFalse();
        verification.Status.Should().Be(McpSkillIntegrityStatus.IntegrityMatch, "not being listed is not a finding");
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectListsWithoutFetchingAnything()
    {
        var server = new FakeSkillsServer { DirectoryRead = true };
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");

        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: false, []);

        report.Servers.Single().Listing!.Skills.Should().ContainSingle();
        report.Servers.Single().Verifications.Should().BeEmpty();
        server.Requests.Should().Equal("server/discover", "skills/list");
    }

    [Fact]
    public async Task AnIncompleteListingIsReportedAsAFloor()
    {
        var server = new FakeSkillsServer { EndlessList = true };

        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: false, []);

        Codes(report).Should().Equal(DiagnosticCodes.McpSkillsPagingIncomplete);
        report.HasIntegrityFindings.Should().BeFalse();
    }

    [Fact]
    public async Task WithoutProbeNothingIsAsked()
    {
        var server = new FakeSkillsServer();
        server.AddStaticSkill(Root, "pdf-processing", "Extract PDFs");

        var report = await Inspector(server).InspectAsync(Config(), probe: false, verify: true, [SkillUri]);

        server.Requests.Should().BeEmpty();
        report.Servers.Single().Detail.Should().Contain("--probe");
    }

    [Fact]
    public async Task AStdioServerIsNeverLaunchedOrAsked()
    {
        var server = new FakeSkillsServer();
        var path = Write("mcp.json", """{ "mcpServers": { "local": { "command": "npx", "args": ["-y", "skills-server"] } } }""");

        var report = await Inspector(server).InspectAsync(path, probe: true, verify: true, []);

        server.Requests.Should().BeEmpty();
        report.Servers.Single().Detail.Should().Contain("never launches");
    }

    [Fact]
    public async Task AServerThatDoesNotDeclareTheExtensionIsNotAskedAboutSkills()
    {
        var server = new FakeSkillsServer { DeclaresSkills = false };

        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: true, []);

        report.Servers.Single().DeclaresSkills.Should().BeFalse();
        report.Servers.Single().Detail.Should().Contain("does not declare io.modelcontextprotocol/skills");
        server.Requests.Should().Equal("server/discover");
    }

    [Fact]
    public async Task AnUnreadableConfigurationIsReportedNotReadAsEmpty()
    {
        var report = await Inspector(new FakeSkillsServer())
            .InspectAsync(Path.Combine(_directory, "absent.json"), probe: true, verify: true, []);

        Codes(report).Should().Equal(DiagnosticCodes.ProviderConfigurationNotParsable);
    }

    [Fact]
    public async Task MeasuredOnFixturesBeforeTheSeveritiesWereFixed()
    {
        // Eight skills on one server: four conforming (two static, one with a nested template, one dynamic) and one
        // each of the four content faults. The numbers are quoted in docs/validation-rules.md; if they stop being
        // true, the build says so.
        var server = new FakeSkillsServer { DirectoryRead = true };
        server.AddStaticSkill("skill://a", "a", "A");
        server.AddStaticSkill("skill://b", "b", "B");
        server.AddStaticSkill("skill://c", "c", "C", ("templates/t.md", "t"));
        server.AddDynamicSkill("skill://d", "d", "D");
        server.AddStaticSkill("skill://tampered", "tampered", "T", ("x.md", "AAAA"));
        server.Files["skill://tampered/x.md"]["text"] = "BBBB";
        server.AddStaticSkill("skill://drift", "drift", "D")["frontmatter"]!["description"] = "Something else";
        server.AddStaticSkill("skill://extra", "extra", "E");
        server.Directories["skill://extra"] = [("skill://extra/SKILL.md", false), ("skill://extra/run.sh", false)];
        server.AddStaticSkill("skill://gone", "gone", "G", ("g.md", "g"));
        server.Files.Remove("skill://gone/g.md");

        foreach (var root in new[] { "skill://a", "skill://b", "skill://d", "skill://tampered", "skill://drift", "skill://gone" })
        {
            server.Directories[root] = [($"{root}/SKILL.md", false)];
        }

        server.Directories["skill://c"] = [("skill://c/SKILL.md", false), ("skill://c/templates", true)];
        server.Directories["skill://c/templates"] = [("skill://c/templates/t.md", false)];
        server.Directories["skill://tampered"] = [("skill://tampered/SKILL.md", false), ("skill://tampered/x.md", false)];
        server.Directories["skill://gone"] = [("skill://gone/SKILL.md", false), ("skill://gone/g.md", false)];

        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: true, []);

        var conforming = report.Diagnostics.Where(finding =>
            finding.Message.Contains("skill://a/", StringComparison.Ordinal)
            || finding.Message.Contains("skill://b/", StringComparison.Ordinal)
            || finding.Message.Contains("skill://c/", StringComparison.Ordinal));

        conforming.Should().BeEmpty("a conforming skill must produce nothing, or the codes cannot go in a pipeline");
        Codes(report).Should().BeEquivalentTo(
        [
            DiagnosticCodes.McpSkillDigestMismatch,
            DiagnosticCodes.McpSkillFrontmatterDrift,
            DiagnosticCodes.McpSkillUnlistedResource,
            DiagnosticCodes.McpSkillMissingResource,
            DiagnosticCodes.McpSkillDynamicIntegrityLimit,
        ]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static IEnumerable<string> Codes(McpSkillsReport report) => report.Diagnostics.Select(finding => finding.Code);

    /// <summary>Serves new text for a file and updates its manifest entry to match, so only the content differs.</summary>
    private static void Reserve(FakeSkillsServer server, string uri, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        server.Files[uri]["text"] = text;

        var entry = server.Skills[SkillUri]["resources"]!.AsArray()
            .OfType<JsonObject>()
            .Single(resource => resource["uri"]!.GetValue<string>() == uri);

        entry["digest"] = FakeSkillsServer.Digest(bytes);
        entry["size"] = bytes.Length;
    }

    private async Task<(McpSkillVerification Verification, McpSkillsReport Report)> VerifyOne(FakeSkillsServer server)
    {
        var report = await Inspector(server).InspectAsync(Config(), probe: true, verify: true, []);

        return (report.Servers.Single().Verifications.Single(), report);
    }

    private string Config() =>
        Write("mcp.json", $$"""{ "mcpServers": { "skills": { "url": "{{FakeSkillsServer.Url}}" } } }""");

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static McpSkillsInspector Inspector(FakeSkillsServer server)
    {
        var http = new HttpClient(server);
        var fileSystem = new FileSystem();
        var client = new Mcp20260728SkillsClient(http);

        var files = new McpFileInspector(
            new IMcpConfigurationReader[]
            {
                new JsonMcpConfigurationReader(fileSystem),
                new TomlMcpConfigurationReader(fileSystem),
            },
            new McpDeclarationInspector(),
            new McpProber([new Mcp20260728ProtocolAdapter(http)]),
            fileSystem);

        var verifier = new McpSkillVerifier(client, new YamlFrontmatterParser(), new Sha256HashCalculator(), TimeProvider.System);

        return new McpSkillsInspector(files, client, verifier);
    }
}
