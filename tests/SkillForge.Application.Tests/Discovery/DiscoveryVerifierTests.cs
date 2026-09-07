using SkillForge.Application.Discovery;
using SkillForge.Application.Mcp;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Discovery;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Discovery;

/// <summary>
/// Declared-versus-runtime comparison, with the probe controlled so the comparison is what is under test.
/// </summary>
/// <remarks>
/// The measurements this file exists for are at the bottom: the new SF82xx codes were counted on fixtures before
/// a severity was fixed, which is the repository's standing rule — a rule is measured before it is published.
/// </remarks>
public sealed class DiscoveryVerifierTests
{
    private static readonly Uri Registry = new("https://registry.example.test/resources");

    [Fact]
    public async Task NothingIsProbedWithoutBeingAsked()
    {
        var adapter = new RecordingAdapter();

        var report = await Verify(adapter, [Mcp("database-tools", ["query"])], probe: false);

        adapter.Probed.Should().BeEmpty("the search is one network request; probing every result is another");

        var resource = report.Resources.Should().ContainSingle().Subject;
        resource.Status.Should().Be(DiscoveryVerificationStatus.NotProbed);
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AMatchingServerIsVerifiedWithoutBeingCalledTrusted()
    {
        var adapter = new RecordingAdapter(Answers(["query", "describe_table"]));

        var report = await Verify(adapter, [Mcp("database-tools", ["query", "describe_table"])]);

        var resource = report.Resources.Should().ContainSingle().Subject;
        resource.Status.Should().Be(DiscoveryVerificationStatus.VerifiedNoDrift);
        resource.Drifts.Should().BeEmpty();
        report.HasDrift.Should().BeFalse();

        // There is no field on the model that could say otherwise, which is what makes the distinction structural
        // rather than a matter of wording.
        typeof(ResourceVerification).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(["Trusted", "Safe", "TrustScore"]);
    }

    [Fact]
    public async Task AToolTheRegistryNeverDeclaredIsReportedAsAWarning()
    {
        // The finding the whole feature exists for: twelve declared, seventeen answered.
        var adapter = new RecordingAdapter(Answers(["query", "describe_table", "delete_database"]));

        var report = await Verify(adapter, [Mcp("database-tools", ["query", "describe_table"])]);

        var resource = report.Resources.Should().ContainSingle().Subject;
        resource.Status.Should().Be(DiscoveryVerificationStatus.DriftDetected);

        var drift = resource.Drifts.Should().ContainSingle().Subject;
        drift.Kind.Should().Be(DiscoveryDriftKind.UnexpectedRuntimeTool);
        drift.Subject.Should().Be("delete_database");
        drift.Declared.Should().BeNull();
        drift.Runtime.Should().Be("delete_database");

        var finding = report.Diagnostics.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.DiscoveryUnexpectedRuntimeTool);
        finding.Severity.Should().Be(DiagnosticSeverity.Warning);
        finding.Message.Should().Contain("delete_database");
    }

    [Fact]
    public async Task EveryFindingSaysWhereBothSidesWereRead()
    {
        var adapter = new RecordingAdapter(Answers(["extra"]));

        var report = await Verify(adapter, [Mcp("database-tools", ["query"])]);

        foreach (var drift in report.Resources.SelectMany(resource => resource.Drifts))
        {
            drift.Evidence.Registry.Should().Be(Registry.ToString());
            drift.Evidence.Endpoint.Should().Be("https://database-tools.example.test/mcp");
            drift.Evidence.ProbedRevision.Should().Be("2026-07-28");
        }

        report.Diagnostics.Should().AllSatisfy(finding =>
            finding.FilePath.Should().Be(Registry.ToString()));
    }

    [Fact]
    public async Task ADeclaredToolTheServerDoesNotHaveIsInformationRatherThanAWarning()
    {
        // Usually a listing that has fallen behind its server. Worth knowing, not worth failing a build over.
        var adapter = new RecordingAdapter(Answers(["query"]));

        var report = await Verify(adapter, [Mcp("database-tools", ["query", "describe_table"])]);

        var drift = report.Resources.Single().Drifts.Should().ContainSingle().Subject;
        drift.Kind.Should().Be(DiscoveryDriftKind.DeclaredToolMissing);

        var finding = report.Diagnostics.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.DiscoveryDeclaredToolMissing);
        finding.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public async Task AMissingToolIsNotReportedWhenTheToolListWasCutShort()
    {
        // A tool absent from an incomplete read might be on the page nobody got to. Reporting it would be a
        // finding produced by SkillForge's own page limit rather than by the server.
        var adapter = new RecordingAdapter(server => McpServerProbe.Answered(
            server,
            ["2026-07-28"],
            ["tools"],
            "Stub",
            null,
            "2026-07-28",
            [Tool("query")],
            new McpToolPaging(100, 1, McpToolPagingOutcome.PageLimitReached)));

        var report = await Verify(adapter, [Mcp("database-tools", ["query", "describe_table"])]);

        report.Resources.Single().Drifts.Should().BeEmpty();
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnexpectedToolIsStillReportedWhenTheListWasCutShort()
    {
        // The asymmetry is the point. A tool that was seen is present whether or not reading finished; a tool that
        // was not seen might simply not have been reached.
        var adapter = new RecordingAdapter(server => McpServerProbe.Answered(
            server,
            ["2026-07-28"],
            ["tools"],
            "Stub",
            null,
            "2026-07-28",
            [Tool("delete_database")],
            new McpToolPaging(100, 1, McpToolPagingOutcome.PageLimitReached)));

        var report = await Verify(adapter, [Mcp("database-tools", ["query"])]);

        report.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.DiscoveryUnexpectedRuntimeTool);
    }

    [Fact]
    public async Task ARegistryThatDeclaresNoCapabilitiesIsNotTreatedAsDeclaringNone()
    {
        // Silence is not a claim. Comparing names against it would make the highest-value finding in the feature
        // fire on every listing that leaves an optional field out.
        var adapter = new RecordingAdapter(Answers(["query", "describe_table", "delete_database"]));

        var report = await Verify(adapter, [Mcp("database-tools", [])]);

        var drift = report.Resources.Single().Drifts.Should().ContainSingle().Subject;
        drift.Kind.Should().Be(DiscoveryDriftKind.ToolCountDrift);
        drift.Declared.Should().Be("no capabilities declared");

        var finding = report.Diagnostics.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.DiscoveryToolCountDrift);
        finding.Severity.Should().Be(DiagnosticSeverity.Info);
        finding.Message.Should().NotContain("unexpected");
    }

    [Fact]
    public async Task ARegistryThatDeclaresNothingAboutAServerWithNoToolsIsSilent()
    {
        var adapter = new RecordingAdapter(Answers([]));

        var report = await Verify(adapter, [Mcp("database-tools", [])]);

        report.Resources.Single().Status.Should().Be(DiscoveryVerificationStatus.VerifiedNoDrift);
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task TwoVersionsThatDisagreeAreReportedAsTwoClaims()
    {
        var adapter = new RecordingAdapter(server => McpServerProbe.Answered(
            server,
            ["2026-07-28"],
            ["tools"],
            "DatabaseTools",
            "3.0.0",
            "2026-07-28",
            [Tool("query")]));

        var report = await Verify(adapter, [Mcp("database-tools", ["query"], version: "1.4.0")]);

        var drift = report.Resources.Single().Drifts.Should().ContainSingle().Subject;
        drift.Kind.Should().Be(DiscoveryDriftKind.SelfReportedVersionDrift);
        drift.Declared.Should().Be("1.4.0");
        drift.Runtime.Should().Be("3.0.0");

        var finding = report.Diagnostics.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.DiscoverySelfReportedVersionDrift);
        finding.Severity.Should().Be(DiagnosticSeverity.Info);
        finding.Suggestion.Should().Contain("self-reported");
    }

    [Fact]
    public async Task AVersionOnOnlySideIsNotADisagreement()
    {
        var adapter = new RecordingAdapter(Answers(["query"]));

        var report = await Verify(adapter, [Mcp("database-tools", ["query"], version: "1.4.0")]);

        report.Resources.Single().Drifts.Should().BeEmpty("the server reported no version to disagree with");
    }

    [Fact]
    public async Task AStdioResourceIsNeverLaunched()
    {
        var adapter = new RecordingAdapter();

        var report = await Verify(
            adapter,
            [Resource("local-tools", DiscoveredResourceType.McpServer, "file:///usr/local/bin/server", [])]);

        adapter.Probed.Should().BeEmpty();

        var resource = report.Resources.Should().ContainSingle().Subject;
        resource.Status.Should().Be(DiscoveryVerificationStatus.UnsupportedResource);
        resource.Detail.Should().Contain("never launches a local server");
    }

    [Fact]
    public async Task ASkillOrWorkflowIsNotVerifiedByRunningIt()
    {
        var adapter = new RecordingAdapter();

        var report = await Verify(
            adapter,
            [
                Resource("deploy", DiscoveredResourceType.Skill, "https://skills.example.test/deploy", []),
                Resource("release", DiscoveredResourceType.Workflow, "https://flows.example.test/release", []),
            ]);

        adapter.Probed.Should().BeEmpty();
        report.Resources.Should().AllSatisfy(resource =>
        {
            resource.Status.Should().Be(DiscoveryVerificationStatus.UnsupportedResource);
            resource.Detail.Should().Contain("without running it");
        });
    }

    [Fact]
    public async Task AServerThatWillNotAnswerIsReportedAsAProbeFailureRatherThanAsAMatch()
    {
        var adapter = new RecordingAdapter(server =>
            McpServerProbe.Failed(server, McpProbeStatus.Unreachable, "connection refused"));

        var report = await Verify(adapter, [Mcp("database-tools", ["query"])]);

        var resource = report.Resources.Should().ContainSingle().Subject;
        resource.Status.Should().Be(DiscoveryVerificationStatus.ProbeFailed);
        resource.Detail.Should().Contain("connection refused");
        resource.Drifts.Should().BeEmpty("nothing was compared, so nothing matched and nothing differed");
    }

    [Fact]
    public async Task ReadingFailuresFromTheSearchAreCarriedThrough()
    {
        var searchFailure = Diagnostic.Warning(
            DiagnosticCodes.DiscoveryResponseNotUsable,
            "'registry' could not be searched.",
            Registry.ToString());

        var report = await new DiscoveryVerifier(new McpProber([new RecordingAdapter()]))
            .VerifyAsync(
                new RemoteDiscoveryResult(Registry, "ard", [], false, [searchFailure]),
                "q",
                probe: true);

        report.Diagnostics.Should().ContainSingle().Which.Code
            .Should().Be(DiagnosticCodes.DiscoveryResponseNotUsable);
    }

    [Fact]
    public async Task ResourcesAndFindingsAreBothOrdered()
    {
        var adapter = new RecordingAdapter(Answers(["zulu", "alpha"]));

        var report = await Verify(
            adapter,
            [Mcp("zebra", []), Mcp("alpha", [])]);

        report.Resources.Select(resource => resource.Resource.Id).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// The measurement the severities were fixed against, run before the codes were published.
    /// </summary>
    /// <remarks>
    /// Eight fixture listings: four that match their servers exactly, two whose servers answer with an extra tool,
    /// one whose listing declares a tool the server dropped, and one that declares no capabilities at all.
    ///
    /// Counts: <c>SF8201</c> fires **2** times — only on the two servers with a genuinely unlisted tool.
    /// <c>SF8202</c> fires **1**. <c>SF8203</c> fires **1**. The four matching listings produce **0** findings
    /// between them, and that is the number that matters: a rule that fired on a correct listing would be a rule
    /// nobody could put in a pipeline. SF8201 is a Warning because two out of eight is a rate a reader will read;
    /// had it fired on all eight it would have had to be information, like SF8003 before it.
    /// </remarks>
    [Fact]
    public async Task MeasuredOnFixturesBeforeTheSeveritiesWereFixed()
    {
        var listings = new[]
        {
            Mcp("match-one", ["query"]),
            Mcp("match-two", ["query"]),
            Mcp("match-three", ["query"]),
            Mcp("match-four", ["query"]),
            Mcp("extra-one", ["query"]),
            Mcp("extra-two", ["query"]),
            Mcp("stale", ["query", "removed_tool"]),
            Mcp("silent", []),
        };

        var adapter = new RecordingAdapter(server => McpServerProbe.Answered(
            server,
            ["2026-07-28"],
            ["tools"],
            "Stub",
            null,
            "2026-07-28",
            server.StartsWith("extra", StringComparison.Ordinal)
                ? [Tool("query"), Tool("delete_everything")]
                : [Tool("query")]));

        var report = await Verify(adapter, listings);

        Count(report, DiagnosticCodes.DiscoveryUnexpectedRuntimeTool).Should().Be(2);
        Count(report, DiagnosticCodes.DiscoveryDeclaredToolMissing).Should().Be(1);
        Count(report, DiagnosticCodes.DiscoveryToolCountDrift).Should().Be(1);

        report.Resources
            .Where(resource => resource.Resource.Id.StartsWith("match", StringComparison.Ordinal))
            .Should().AllSatisfy(resource => resource.Drifts.Should().BeEmpty());
    }

    private static int Count(DiscoveryVerificationReport report, string code) =>
        report.Diagnostics.Count(finding => finding.Code == code);

    private static Task<DiscoveryVerificationReport> Verify(
        RecordingAdapter adapter,
        IReadOnlyList<DiscoveredResource> resources,
        bool probe = true) =>
        new DiscoveryVerifier(new McpProber([adapter]))
            .VerifyAsync(new RemoteDiscoveryResult(Registry, "ard", resources, false, []), "q", probe);

    private static Func<string, McpServerProbe> Answers(IReadOnlyList<string> tools) =>
        server => McpServerProbe.Answered(
            server,
            ["2026-07-28"],
            ["tools"],
            "Stub",
            null,
            "2026-07-28",
            [.. tools.Select(Tool)]);

    private static McpToolSummary Tool(string name) => new(name, true, null, []);

    private static DiscoveredResource Mcp(
        string name,
        IReadOnlyList<string> capabilities,
        string? version = null) =>
        Resource(
            name,
            DiscoveredResourceType.McpServer,
            $"https://{name}.example.test/mcp",
            capabilities,
            version);

    private static DiscoveredResource Resource(
        string name,
        DiscoveredResourceType type,
        string endpoint,
        IReadOnlyList<string> capabilities,
        string? version = null) =>
        new(
            name,
            name,
            type,
            new Uri(endpoint),
            "Example Inc",
            version,
            Registry.ToString(),
            [.. capabilities.Select(capability =>
                new DeclaredCapability(capability, DeclaredCapabilityKind.Tool, null))],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private sealed class RecordingAdapter(Func<string, McpServerProbe>? answer = null) : IMcpProtocolAdapter
    {
        internal List<string> Probed { get; } = [];

        public string ProtocolVersion => "2026-07-28";

        public Task<McpServerProbe> ProbeAsync(
            McpServerDeclaration server,
            CancellationToken cancellationToken = default)
        {
            Probed.Add(server.Name);

            return Task.FromResult(answer is null
                ? McpServerProbe.Answered(server.Name, ["2026-07-28"], ["tools"], "Stub", null, "2026-07-28", [])
                : answer(server.Name));
        }
    }
}
