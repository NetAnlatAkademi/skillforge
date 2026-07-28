using SkillForge.Application.Mcp;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Mcp;

/// <summary>
/// The prober decides what may be asked at all, which is the safety-relevant half of this feature.
/// </summary>
public sealed class McpProberTests
{
    [Fact]
    public async Task NeverAsksAStdioServerAndSaysWhy()
    {
        // Inspecting a local server by launching it would argue with the whole product. The reason is recorded so a
        // reader can tell "was never asked" from "did not answer".
        var adapter = new RecordingAdapter();

        var outcome = await new McpProber([adapter]).ProbeAsync([Stdio("local")]);

        adapter.Probed.Should().BeEmpty();
        var probe = outcome.Probes.Should().ContainSingle().Subject;
        probe.Status.Should().Be(McpProbeStatus.NotProbed);
        probe.Detail.Should().Contain("never launches a local command");
    }

    [Fact]
    public async Task AsksAnHttpServer()
    {
        var adapter = new RecordingAdapter();

        await new McpProber([adapter]).ProbeAsync([Http("remote")]);

        adapter.Probed.Should().Equal("remote");
    }

    [Fact]
    public async Task ReportsSF8004WhenTheServerHasNoDiscovery()
    {
        var adapter = new RecordingAdapter(server =>
            McpServerProbe.Failed(server, McpProbeStatus.NoDiscovery, "method not found"));

        var outcome = await new McpProber([adapter]).ProbeAsync([Http("legacy")]);

        var finding = outcome.Diagnostics.Should().ContainSingle().Subject;
        finding.Code.Should().Be(DiagnosticCodes.McpNoDiscovery);
        finding.Severity.Should().Be(DiagnosticSeverity.Info);
        finding.Suggestion.Should().Contain("Not a fault");
    }

    [Fact]
    public async Task ReportsSF8005ForADeprecatedServerCapability()
    {
        var adapter = new RecordingAdapter(server =>
            McpServerProbe.Answered(server, ["2026-07-28"], ["logging", "tools"], "Stub", "1.0"));

        var outcome = await new McpProber([adapter]).ProbeAsync([Http("modern")]);

        outcome.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.McpDeprecatedCapability);
    }

    [Fact]
    public async Task DoesNotLookForClientCapabilitiesAServerCannotDeclare()
    {
        // Roots and Sampling were deprecated by the same SEP as Logging and are listed beside it everywhere, but they
        // are client capabilities. Looking for them on a server would be a check that can never fire — the kind of rule
        // this project measures its way out of.
        var adapter = new RecordingAdapter(server =>
            McpServerProbe.Answered(server, ["2026-07-28"], ["roots", "sampling", "tools"], null, null));

        var outcome = await new McpProber([adapter]).ProbeAsync([Http("modern")]);

        outcome.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task SaysSoWhenNoAdapterIsRegistered()
    {
        var outcome = await new McpProber([]).ProbeAsync([Http("remote")]);

        var probe = outcome.Probes.Should().ContainSingle().Subject;
        probe.Status.Should().Be(McpProbeStatus.NotProbed);
        probe.Detail.Should().Contain("no protocol adapter");
    }

    [Fact]
    public async Task KeepsProbingAfterOneServerCannotBeReached()
    {
        // One unreachable server is a fact about that server, not a reason to abandon the inventory.
        var adapter = new RecordingAdapter(server => server == "broken"
            ? McpServerProbe.Failed(server, McpProbeStatus.Unreachable, "connection refused")
            : McpServerProbe.Answered(server, ["2026-07-28"], ["tools"], null, null));

        var outcome = await new McpProber([adapter]).ProbeAsync([Http("broken"), Http("fine")]);

        outcome.Probes.Select(probe => probe.Status)
            .Should().Equal(McpProbeStatus.Unreachable, McpProbeStatus.Answered);
    }

    private static McpServerDeclaration Stdio(string name) =>
        new(name, "claude-code", McpTransport.Stdio, "npx", ["-y", "some-mcp"], [], "/home/dev/.claude.json");

    private static McpServerDeclaration Http(string name) =>
        new(name, "claude-code", McpTransport.Http, "https://example.test/mcp", [], [], "/home/dev/.claude.json");

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
                ? McpServerProbe.Answered(server.Name, ["2026-07-28"], ["tools"], "Stub", "1.0")
                : answer(server.Name));
        }
    }
}
