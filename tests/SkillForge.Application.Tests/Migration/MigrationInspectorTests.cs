using SkillForge.Application.Migration;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Migration;

/// <summary>
/// The inspector's whole job is to run the adapters and merge what they say, in an order that does not depend on
/// how they were registered.
/// </summary>
public sealed class MigrationInspectorTests
{
    [Fact]
    public async Task RunsEveryAdapterAndKeepsProvidersInIdentifierOrder()
    {
        var inspector = new MigrationInspector(
        [
            new StubAdapter("github-copilot"),
            new StubAdapter("claude-code"),
            new StubAdapter("codex"),
        ]);

        var inspection = await inspector.InspectAsync(new AgentToolScanRequest("/home/dev", null));

        inspection.Providers.Select(provider => provider.ProviderId)
            .Should().Equal("claude-code", "codex", "github-copilot");
    }

    [Fact]
    public async Task ReportsAProviderThatWasNotFoundRatherThanLeavingItOut()
    {
        // The absence is the answer to "can I move to it?", and omitting it would look like it was not looked for.
        var inspector = new MigrationInspector([new StubAdapter("cursor", present: false)]);

        var inspection = await inspector.InspectAsync(new AgentToolScanRequest("/home/dev", null));

        inspection.Providers.Should().ContainSingle().Which.IsPresent.Should().BeFalse();
        inspection.PresentProviders.Should().BeEmpty();
    }

    [Fact]
    public async Task MergesSkillsServersInstructionsAndDiagnostics()
    {
        var inspector = new MigrationInspector(
        [
            new StubAdapter("claude-code", skillName: "one", serverName: "server-one"),
            new StubAdapter("codex", skillName: "two", unreadablePath: "/home/dev/.codex/config.toml"),
        ]);

        var inspection = await inspector.InspectAsync(new AgentToolScanRequest("/home/dev", "/work/repo"));

        inspection.UserDirectory.Should().Be("/home/dev");
        inspection.ProjectDirectory.Should().Be("/work/repo");
        inspection.Skills.Select(skill => skill.Name).Should().Equal("one", "two");
        inspection.McpServers.Should().ContainSingle().Which.Name.Should().Be("server-one");
        inspection.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderConfigurationNotParsable);
    }

    [Fact]
    public async Task LetsAnAdapterBugSurfaceRatherThanHidingIt()
    {
        // Same stance as the validator: a throwing adapter is a bug, and swallowing it would hand the user a
        // quietly incomplete inventory. A file it merely cannot parse is SF1015 instead.
        var inspector = new MigrationInspector([new ThrowingAdapter()]);

        var act = async () => await inspector.InspectAsync(new AgentToolScanRequest("/home/dev", null));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubAdapter(
        string providerId,
        bool present = true,
        string? skillName = null,
        string? serverName = null,
        string? unreadablePath = null) : IAgentToolAdapter
    {
        public string ProviderId => providerId;

        public Task<AgentToolScan> ScanAsync(
            AgentToolScanRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentToolScan(
                new AgentToolPresence(providerId, providerId, present, present ? ["/some/path"] : []),
                skillName is null ? [] : [new SkillInventoryEntry(providerId, skillName, "/skills", [])],
                serverName is null
                    ? []
                    : [new McpServerDeclaration(
                        serverName, providerId, McpTransport.Stdio, "node", [], [], "/config")],
                [],
                unreadablePath is null
                    ? []
                    : McpConfigurationReadResult.Unreadable(unreadablePath, "stub").Diagnostics));
    }

    private sealed class ThrowingAdapter : IAgentToolAdapter
    {
        public string ProviderId => "broken";

        public Task<AgentToolScan> ScanAsync(
            AgentToolScanRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("adapter bug");
    }
}
