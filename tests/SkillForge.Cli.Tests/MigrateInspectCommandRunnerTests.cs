using System.Text.Json;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Migration;
using SkillForge.Cli.Commands;
using SkillForge.Domain.Migration;

namespace SkillForge.Cli.Tests;

/// <summary>
/// <c>migrate inspect</c> describes and does not judge, so what these pin down is the shape of the output and the
/// one guarantee that must never regress: an environment variable's value is never written anywhere.
/// </summary>
public sealed class MigrateInspectCommandRunnerTests
{
    private const string SecretValue = "super-secret-token-value";

    [Fact]
    public async Task AnInventoryExitsZeroEvenWhenItListsPlenty()
    {
        var runner = Build(out _, Inspection());

        (await runner.RunAsync(Request(), CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task TheConsoleReportNamesProvidersSkillsServersAndInstructionFiles()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, Inspection(), fileSystem);

        await runner.RunAsync(Request(outputPath: "/out/report.txt"), CancellationToken.None);

        var text = fileSystem.ReadText("/out/report.txt");
        text.Should().Contain("Claude Code");
        text.Should().Contain("demo-skill");
        text.Should().Contain("azure-devops");
        text.Should().Contain("CLAUDE.md");
        text.Should().Contain("inventory, not a verdict");
    }

    [Fact]
    public async Task TheConsoleReportPrintsEnvironmentVariableNamesAndNeverValues()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, Inspection(), fileSystem);

        await runner.RunAsync(Request(outputPath: "/out/report.txt"), CancellationToken.None);

        var text = fileSystem.ReadText("/out/report.txt");
        text.Should().Contain("AZURE_TOKEN", "the name answers what a move would need");
        text.Should().NotContain(SecretValue);
    }

    [Fact]
    public async Task TheJsonDocumentCarriesTheInventoryAndNoEnvironmentValues()
    {
        var fileSystem = new FakeFileSystem();
        var runner = Build(out _, Inspection(), fileSystem);

        await runner.RunAsync(
            Request(format: OutputFormat.Json, outputPath: "/out/report.json"),
            CancellationToken.None);

        var json = fileSystem.ReadText("/out/report.json");
        json.Should().NotContain(SecretValue);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        root.GetProperty("providers").GetArrayLength().Should().Be(1);
        root.GetProperty("skills")[0].GetProperty("name").GetString().Should().Be("demo-skill");

        var server = root.GetProperty("mcpServers")[0];
        server.GetProperty("transport").GetString().Should().Be("stdio");
        server.GetProperty("environmentVariableNames")[0].GetString().Should().Be("AZURE_TOKEN");
        server.TryGetProperty("environment", out _).Should().BeFalse("there is no place for a value to hide");
    }

    [Fact]
    public async Task ReadsTheCurrentUsersHomeDirectoryUnlessToldOtherwise()
    {
        var runner = Build(out var inspector, Inspection());

        await runner.RunAsync(Request(), CancellationToken.None);
        inspector.LastRequest!.UserDirectory.Should().Be("/home/stub");

        await runner.RunAsync(Request(userDirectory: "/exported/profile"), CancellationToken.None);
        inspector.LastRequest!.UserDirectory.Should().Be("/exported/profile");
    }

    [Fact]
    public async Task OnlyLooksAtAProjectWhenOneWasNamed()
    {
        var runner = Build(out var inspector, Inspection());

        await runner.RunAsync(Request(), CancellationToken.None);
        inspector.LastRequest!.ProjectDirectory.Should().BeNull();

        await runner.RunAsync(Request(projectPath: "/work/repo"), CancellationToken.None);
        inspector.LastRequest!.ProjectDirectory.Should().Be("/work/repo");
    }

    [Fact]
    public void RejectsMissingDependencies()
    {
        var act = () => new MigrateInspectCommandRunner(null!, null!, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static MigrationInspection Inspection() =>
        new(
            "/home/stub",
            null,
            [new AgentToolPresence("claude-code", "Claude Code", true, ["/home/stub/.claude"])],
            [new SkillInventoryEntry("claude-code", "demo-skill", "/home/stub/.claude/skills/demo", ["codex"])],
            [new McpServerDeclaration(
                "azure-devops",
                "claude-code",
                McpTransport.Stdio,
                "npx",
                ["-y", "@azure-devops/mcp"],
                ["AZURE_TOKEN"],
                "/home/stub/.claude.json")],
            [new InstructionFileReference(
                "claude-code",
                "/home/stub/.claude/CLAUDE.md",
                InstructionScope.User,
                120)],
            []);

    private static MigrateInspectRequest Request(
        string? projectPath = null,
        string? userDirectory = null,
        string format = OutputFormat.Console,
        string? outputPath = null) =>
        new(projectPath, userDirectory, format, outputPath, new ReportRenderOptions());

    private static MigrateInspectCommandRunner Build(
        out StubInspector inspector,
        MigrationInspection inspection,
        FakeFileSystem? fileSystem = null)
    {
        inspector = new StubInspector(inspection);

        return new MigrateInspectCommandRunner(
            inspector,
            new StubUserEnvironment(),
            fileSystem ?? new FakeFileSystem());
    }

    private sealed class StubInspector(MigrationInspection inspection) : IMigrationInspector
    {
        internal AgentToolScanRequest? LastRequest { get; private set; }

        public Task<MigrationInspection> InspectAsync(
            AgentToolScanRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(inspection);
        }
    }

    private sealed class StubUserEnvironment : IUserEnvironment
    {
        public string HomeDirectory => "/home/stub";
    }
}
