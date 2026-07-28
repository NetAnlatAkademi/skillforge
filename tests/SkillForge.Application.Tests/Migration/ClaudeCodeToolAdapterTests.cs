using SkillForge.Application.Migration;
using SkillForge.Application.Migration.Adapters;
using SkillForge.Application.Tests.Fakes;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Migration;

/// <summary>
/// An adapter is where one provider's paths live, so these tests are about paths: which ones make it "present",
/// and which are read at user scope versus project scope. What a JSON or TOML file says is the readers' business.
/// </summary>
public sealed class ClaudeCodeToolAdapterTests
{
    private const string Home = "/home/dev";
    private const string Project = "/work/repo";

    [Fact]
    public async Task ReportsTheProviderAsAbsentWhenNothingIsInstalled()
    {
        var scan = await Scan(new FakeFileSystem(), new FakeSkillLoader(), new FakeMcpConfigurationReader());

        scan.Presence.ProviderId.Should().Be("claude-code");
        scan.Presence.IsPresent.Should().BeFalse();
        scan.Presence.ConfigurationPaths.Should().BeEmpty();
        scan.Skills.Should().BeEmpty();
        scan.McpServers.Should().BeEmpty();
        scan.InstructionFiles.Should().BeEmpty();
        scan.Diagnostics.Should().BeEmpty("absence is an answer, not a failure");
    }

    [Fact]
    public async Task IsPresentOnceAnyOfItsOwnPathsExists()
    {
        var files = new FakeFileSystem().AddDirectory($"{Home}/.claude");

        var scan = await Scan(files, new FakeSkillLoader(), new FakeMcpConfigurationReader());

        scan.Presence.IsPresent.Should().BeTrue();

        // Path.Combine uses the platform separator, so the expectation is built the same way rather than written
        // with a forward slash that only matches on Linux.
        scan.Presence.ConfigurationPaths.Should().Equal(Path.Combine(Home, ".claude"));
    }

    [Fact]
    public async Task FindsSkillsInstalledAtUserScopeWithWhatTheyDeclare()
    {
        var files = new FakeFileSystem().AddFile($"{Home}/.claude/skills/demo/SKILL.md", "---\n---\n");
        var loader = new FakeSkillLoader().WithSkill($"{Home}/.claude/skills/demo", "demo", "claude-code");

        var scan = await Scan(files, loader, new FakeMcpConfigurationReader());

        var skill = scan.Skills.Should().ContainSingle().Subject;
        skill.Name.Should().Be("demo");
        skill.ProviderId.Should().Be("claude-code");
        skill.DeclaredCompatibility.Should().Equal("claude-code");
    }

    [Fact]
    public async Task ListsASkillThatFailsToLoadUnderItsDirectoryNameRatherThanOmittingIt()
    {
        // It is installed either way, and an inventory that hides it is wrong about the machine. Judging it is
        // what validate is for.
        var files = new FakeFileSystem().AddFile($"{Home}/.claude/skills/broken/SKILL.md", "not frontmatter");

        var scan = await Scan(files, new FakeSkillLoader(), new FakeMcpConfigurationReader());

        scan.Skills.Should().ContainSingle().Which.Name.Should().Be("broken");
    }

    [Fact]
    public async Task IgnoresADirectoryThatHoldsNoSkillFile()
    {
        var files = new FakeFileSystem()
            .AddDirectory($"{Home}/.claude/skills/not-a-skill")
            .AddFile($"{Home}/.claude/skills/real/SKILL.md", "---\n---\n");

        var scan = await Scan(files, new FakeSkillLoader(), new FakeMcpConfigurationReader());

        scan.Skills.Should().ContainSingle().Which.Name.Should().Be("real");
    }

    [Fact]
    public async Task ReadsMcpServersFromTheUserFileThenTheProjectFile()
    {
        var files = new FakeFileSystem()
            .AddFile($"{Home}/.claude.json", "{}")
            .AddFile($"{Project}/.mcp.json", "{}");

        var readers = new FakeMcpConfigurationReader()
            .WithServer($"{Home}/.claude.json", "user-server")
            .WithServer($"{Project}/.mcp.json", "repo-server");

        var scan = await Scan(files, new FakeSkillLoader(), readers, Project);

        scan.McpServers.Select(server => server.Name).Should().Equal("user-server", "repo-server");
        scan.McpServers.Should().AllSatisfy(server => server.ProviderId.Should().Be("claude-code"));
    }

    [Fact]
    public async Task DoesNotLookAtProjectScopeWhenNoProjectWasGiven()
    {
        var files = new FakeFileSystem()
            .AddFile($"{Project}/.mcp.json", "{}")
            .AddFile($"{Project}/CLAUDE.md", "project instructions");

        var readers = new FakeMcpConfigurationReader().WithServer($"{Project}/.mcp.json", "repo-server");

        var scan = await Scan(files, new FakeSkillLoader(), readers);

        scan.McpServers.Should().BeEmpty();
        scan.InstructionFiles.Should().BeEmpty();
        readers.ReadPaths.Should().NotContain($"{Project}/.mcp.json");
    }

    [Fact]
    public async Task FindsInstructionFilesAtBothScopes()
    {
        var files = new FakeFileSystem()
            .AddFile($"{Home}/.claude/CLAUDE.md", "user instructions")
            .AddFile($"{Home}/.claude/AGENTS.md", "shared instructions")
            .AddFile($"{Project}/CLAUDE.md", "project instructions");

        var scan = await Scan(files, new FakeSkillLoader(), new FakeMcpConfigurationReader(), Project);

        scan.InstructionFiles.Select(file => file.Scope)
            .Should().Equal(InstructionScope.User, InstructionScope.User, InstructionScope.Project);
        scan.InstructionFiles.Should().AllSatisfy(file => file.SizeInBytes.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ReportsAConfigurationItCannotParseInsteadOfPresentingAnEmptyInventory()
    {
        var files = new FakeFileSystem().AddFile($"{Home}/.claude.json", "{ not json");
        var readers = new FakeMcpConfigurationReader().WithUnreadable($"{Home}/.claude.json");

        var scan = await Scan(files, new FakeSkillLoader(), readers);

        scan.Diagnostics.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ProviderConfigurationNotParsable);
    }

    [Fact]
    public void RejectsMissingDependencies()
    {
        var act = () => new ClaudeCodeToolAdapter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static async Task<AgentToolScan> Scan(
        FakeFileSystem files,
        FakeSkillLoader loader,
        FakeMcpConfigurationReader readers,
        string? project = null)
    {
        var scanner = new AgentToolScanner(
            files,
            new SkillInventoryScanner(files, loader),
            new McpConfigurationScanner(files, [readers]),
            new InstructionFileScanner(files));

        return await new ClaudeCodeToolAdapter(scanner)
            .ScanAsync(new AgentToolScanRequest(Home, project), CancellationToken.None);
    }
}
