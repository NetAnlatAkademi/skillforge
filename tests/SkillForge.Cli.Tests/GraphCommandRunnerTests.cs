using SkillForge.Application.Graph;
using SkillForge.Application.Inspection;
using SkillForge.Application.Skills;
using SkillForge.Cli.Commands;
using SkillForge.Infrastructure;
using SkillForge.Infrastructure.Migration;
using SkillForge.Infrastructure.Provenance;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Cli.Tests;

/// <summary>
/// <c>graph</c> over a real directory.
/// </summary>
/// <remarks>
/// End to end against the real readers, for the reason the update tests give: the interesting failures are in the
/// reading — a plugin manifest three directories up, an <c>allowed-tools</c> entry that names a server, a hook
/// file that mentions a script path. A fake that returned those relationships would be testing the fake.
/// </remarks>
public sealed class GraphCommandRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-graph-tests",
        Guid.NewGuid().ToString("n"));

    public GraphCommandRunnerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ADirectoryWithNothingInItProducesAnEmptyGraphAndSucceeds()
    {
        var report = await Graph();

        report.Should().Contain("Nodes: 0");
        report.Should().Contain("(nothing the graph knows about was found)");
        (await Run()).Should().Be(0);
    }

    [Fact]
    public async Task ASkillIsANodeAndItsScriptIsContained()
    {
        WriteSkill("deploy-skill");
        WriteScript("deploy-skill", "deploy.sh");

        var report = await Graph();

        report.Should().Contain("deploy-skill");
        report.Should().Contain("--Contains--> skills/deploy-skill/scripts/deploy.sh");
        report.Should().Contain("executable file in skill directory");
    }

    [Fact]
    public async Task AnMcpServerReachesItsHostAndItsCredentialName()
    {
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");

        var report = await Graph();

        report.Should().Contain("production-mcp --ConnectsTo--> prod.company.com");
        report.Should().Contain("production-mcp --ReadsCredential--> DEPLOY_TOKEN");
    }

    [Fact]
    public async Task ACredentialValueIsNeverInTheGraph()
    {
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");

        foreach (var format in new[] { OutputFormat.Console, OutputFormat.Json, OutputFormat.Mermaid })
        {
            var report = await Graph(format);

            report.Should().Contain("DEPLOY_TOKEN", "the name is the whole point of the node");
            report.Should().NotContain(Secret, "no value is ever read into the model");
        }
    }

    [Fact]
    public async Task AnAllowedToolsEntryIsADeclaredEdgeWithItsOwnLine()
    {
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        WriteSkill("deploy-skill", allowedTool: "mcp__production-mcp__deploy");

        var report = await Graph();

        report.Should().Contain("deploy-skill --Invokes--> production-mcp");
        report.Should().Contain("declared · allowed-tools entry · skills/deploy-skill/SKILL.md:5");
    }

    [Fact]
    public async Task AServerNamedOnlyInTheBodyIsAnInferredEdge()
    {
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        WriteSkill("deploy-skill", body: "The production-mcp server performs the rollout.");

        var report = await Graph();

        report.Should().Contain("deploy-skill --Invokes--> production-mcp");
        report.Should().Contain("inferred · server name written in the skill body");
    }

    [Fact]
    public async Task ADeclaredEdgeBeatsAnInferredOneForTheSamePair()
    {
        // Both readings are true, and the stronger evidence is the one worth printing. Two edges for one
        // relationship would double every count a reader takes off the diagram.
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        WriteSkill(
            "deploy-skill",
            allowedTool: "mcp__production-mcp__deploy",
            body: "The production-mcp server performs the rollout.");

        var report = await Graph();

        report.Should().Contain("declared · allowed-tools entry");
        report.Should().NotContain("server name written in the skill body");
    }

    [Fact]
    public async Task AShortServerNameIsNeverMatchedInProse()
    {
        // A server called "db" matches inside "database", "dbo" and half the English language. An edge produced
        // that way is noise with a citation attached, which is worse than no edge.
        WriteMcp("db", "https://db.company.com/mcp", "DB_TOKEN");
        WriteSkill("deploy-skill", body: "Update the database before shipping.");

        var report = await Graph();

        report.Should().NotContain("deploy-skill --Invokes--> db");
    }

    [Fact]
    public async Task ASkillInsideAPluginIsContainedByItAndDistributedByIt()
    {
        WritePlugin("acme-agent-pack", "https://github.com/acme/agent-pack");
        WriteSkill("deploy-skill");

        var report = await Graph();

        report.Should().Contain("acme-agent-pack --Contains--> deploy-skill");
        report.Should().Contain("deploy-skill --DistributedBy--> acme-agent-pack");
        report.Should().Contain("acme-agent-pack --UpdatesFrom--> github.com");
    }

    [Fact]
    public async Task AHookThatNamesAScriptPathExecutesIt()
    {
        WriteSkill("deploy-skill");
        WriteScript("deploy-skill", "deploy.sh");
        WriteHook("skills/deploy-skill/scripts/deploy.sh --check");

        var report = await Graph();

        report.Should().Contain("--Executes--> skills/deploy-skill/scripts/deploy.sh");
        report.Should().Contain("script path written in the hook configuration");
    }

    [Fact]
    public async Task AnInstructionFileThatNamesAServerReferencesIt()
    {
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        File.WriteAllText(
            Path.Combine(_root, "CLAUDE.md"),
            "# Instructions\n\nAlways ask a human before calling production-mcp.\n");

        var report = await Graph();

        report.Should().Contain("CLAUDE.md --References--> production-mcp");
        report.Should().Contain("server name written in the instruction file · CLAUDE.md:3");
    }

    [Fact]
    public async Task NoApprovalBoundaryIsInventedFromProseThatAsksForOne()
    {
        // The instruction above says "always ask a human", which is exactly the sentence an inference would fire
        // on. A boundary on the diagram that nothing enforces is a safety control nobody can appeal.
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        File.WriteAllText(
            Path.Combine(_root, "CLAUDE.md"),
            "Always ask the user for approval before deploying to production.\n");

        var report = await Graph();

        report.Should().NotContain("ApprovalBoundary");
        report.Should().NotContain("ApprovedBy");
    }

    [Fact]
    public async Task ADeclaredApprovalBoundaryIsDrawn()
    {
        WriteSkill("deploy-skill");
        WriteSkillConfiguration(
            "deploy-skill",
            """
            approval:
              required: true
              before:
                - production deploy
            """);

        var report = await Graph();

        report.Should().Contain("ApprovalBoundary (1)");
        report.Should().Contain("deploy-skill --ApprovedBy--> human approval: production deploy");
        report.Should().Contain("approval.required in skillforge.yaml");
    }

    [Fact]
    public async Task ApprovalDeclaredFalseDrawsNothing()
    {
        // A declaration that there is no boundary is useful and is not a node: a node for the absence of a thing
        // is not a node.
        WriteSkill("deploy-skill");
        WriteSkillConfiguration(
            "deploy-skill",
            """
            approval:
              required: false
            """);

        var report = await Graph();

        report.Should().NotContain("ApprovalBoundary");
    }

    [Fact]
    public async Task ProseThatAsksForApprovalDrawsNoBoundary()
    {
        // The sentence a naive inference would fire on, in the file it would read. Nothing enforces it, so
        // nothing draws it.
        WriteSkill(
            "deploy-skill",
            body: "IMPORTANT: always ask the user for approval before deploying to production. Require human "
                + "sign-off. This is an approval boundary.");

        var report = await Graph();

        report.Should().NotContain("ApprovalBoundary");
        report.Should().NotContain("ApprovedBy");
    }

    [Fact]
    public async Task NothingIsClassifiedAsProductionFromAHostName()
    {
        // "prod.company.com" is the name of a host, not evidence about an environment. Classifying it would put a
        // claim on the diagram that came from a string somebody chose.
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");

        var report = await Graph(OutputFormat.Json);

        report.Should().NotContain("ExecutionEnvironment");
        report.Should().NotContain("Production");
    }

    [Fact]
    public async Task TheGraphIsIdenticalBetweenTwoRuns()
    {
        WritePlugin("acme-agent-pack", "https://github.com/acme/agent-pack");
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        WriteSkill("deploy-skill", allowedTool: "mcp__production-mcp__deploy");
        WriteScript("deploy-skill", "deploy.sh");
        WriteHook("skills/deploy-skill/scripts/deploy.sh");

        foreach (var format in new[] { OutputFormat.Console, OutputFormat.Json, OutputFormat.Mermaid })
        {
            var first = await Graph(format);
            var second = await Graph(format);

            second.Should().Be(first, "a graph that differs between two runs cannot be diffed in a pipeline");
        }
    }

    [Fact]
    public async Task MermaidIdentifiersAreSafeAndLabelsAreQuoted()
    {
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");

        var diagram = await Graph(OutputFormat.Mermaid);

        diagram.Should().StartWith("graph LR");
        diagram.Should().Contain("(\"prod.company.com\")");

        // The dots and the colon in "mcp:production-mcp" would end a Mermaid identifier.
        diagram.Should().NotContain("mcp:production-mcp");
        diagram.Should().MatchRegex(@"\n  [a-z0-9_]+\[""production-mcp""\]");
    }

    [Fact]
    public async Task AMermaidLabelCannotBreakOutOfItsOwnQuotes()
    {
        // A server name comes from a configuration file, which is not a friendly source of text.
        WriteMcp("say \"hi\" #now", "https://odd.company.com/mcp", "ODD_TOKEN");

        var diagram = await Graph(OutputFormat.Mermaid);

        diagram.Should().Contain("say 'hi' _now");
        diagram.Should().NotContain("\"hi\"");
    }

    [Fact]
    public async Task AnUnparsableMcpConfigurationFailsTheRunRatherThanLosingANode()
    {
        File.WriteAllText(Path.Combine(_root, ".mcp.json"), "{ not json");

        (await Run()).Should().Be(1);
        (await Graph()).Should().Contain("Could not be read:");
    }

    [Fact]
    public async Task AGraphIsNeverAFinding()
    {
        WritePlugin("acme-agent-pack", "https://github.com/acme/agent-pack");
        WriteMcp("production-mcp", "https://prod.company.com/mcp", "DEPLOY_TOKEN");
        WriteSkill("deploy-skill", allowedTool: "mcp__production-mcp__deploy");

        (await Run()).Should().Be(0, "a description exits zero however alarming the shape it describes");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private const string Secret = "sk-live-do-not-print-me";

    private async Task<string> Graph(string format = OutputFormat.Console)
    {
        var output = Path.Combine(_root, "graph.out");

        await Run(format, output);

        return await File.ReadAllTextAsync(output);
    }

    private async Task<int> Run(string format = OutputFormat.Console, string? output = null)
    {
        var fileSystem = new FileSystem();
        var loader = new SkillLoader(
            fileSystem,
            new YamlFrontmatterParser(),
            new YamlSkillConfigurationReader(fileSystem));

        var runner = new GraphCommandRunner(
            new GraphBuilder(
                fileSystem,
                new SkillDiscovery(fileSystem),
                loader,
                new SkillInspector(),
                [new JsonMcpConfigurationReader(fileSystem)],
                new JsonDistributionManifestReader(fileSystem)),
            fileSystem);

        return await runner.RunAsync(
            new GraphRequest(_root, format, output, new Application.Abstractions.ReportRenderOptions()),
            CancellationToken.None);
    }

    private void WriteSkill(string name, string? allowedTool = null, string? body = null)
    {
        var directory = Path.Combine(_root, "skills", name);
        Directory.CreateDirectory(directory);

        // Five lines of frontmatter when a tool is declared, which is what the line assertion above pins.
        var tools = allowedTool is null
            ? "license: MIT"
            : $"allowed-tools:\n  - {allowedTool}";

        File.WriteAllText(
            Path.Combine(directory, "SKILL.md"),
            $"""
            ---
            name: {name}
            description: Use this skill when drawing a graph in a test that needs a real skill on disk.
            {tools}
            ---

            # {name}

            {body ?? "Nothing in particular."}
            """);
    }

    private void WriteSkillConfiguration(string skill, string yaml)
    {
        File.WriteAllText(Path.Combine(_root, "skills", skill, "skillforge.yaml"), yaml);
    }

    private void WriteScript(string skill, string fileName)
    {
        var directory = Path.Combine(_root, "skills", skill, "scripts");
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, fileName), "echo hello");
    }

    private void WriteMcp(string server, string url, string environmentVariable)
    {
        File.WriteAllText(
            Path.Combine(_root, ".mcp.json"),
            $$"""
            {
              "mcpServers": {
                {{System.Text.Json.JsonSerializer.Serialize(server)}}: {
                  "type": "http",
                  "url": "{{url}}",
                  "env": { "{{environmentVariable}}": "{{Secret}}" }
                }
              }
            }
            """);
    }

    private void WritePlugin(string name, string repository)
    {
        var directory = Path.Combine(_root, ".claude-plugin");
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "plugin.json"),
            $$"""
            { "name": "{{name}}", "version": "1.0.0", "author": "Acme", "repository": "{{repository}}" }
            """);
    }

    private void WriteHook(string command)
    {
        var directory = Path.Combine(_root, "hooks");
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "hooks.json"),
            $$"""
            {
              "hooks": {
                "PreToolUse": [
                  { "hooks": [ { "type": "command", "command": "{{command}}" } ] }
                ]
              }
            }
            """);
    }
}
