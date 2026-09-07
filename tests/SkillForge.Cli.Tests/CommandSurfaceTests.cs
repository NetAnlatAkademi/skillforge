using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Validation;
using SkillForge.Cli.Commands;

namespace SkillForge.Cli.Tests;

/// <summary>
/// Smoke tests over the command surface: the things a user types first, and the parse errors that must map
/// to exit code 2 rather than looking like a validation failure.
/// </summary>
public sealed class CommandSurfaceTests
{
    private static RootCommand Root()
    {
        var services = CompositionRoot.Build();
        return SkillForgeCommandLine.Build(services);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    [InlineData("validate --help")]
    [InlineData("validate ./samples/valid-skill")]
    [InlineData("validate ./samples/valid-skill --strict")]
    [InlineData("validate --quiet --no-color ./samples/valid-skill")]
    [InlineData("validate ./samples/valid-skill --provider claude-code,codex")]
    [InlineData("validate ./samples/valid-skill --provider claude-code --provider codex")]
    [InlineData("validate ./samples/valid-skill --suppress SF7001")]
    [InlineData("validate ./samples/valid-skill --provider some-future-agent")]
    [InlineData("migrate inspect")]
    [InlineData("migrate inspect .")]
    [InlineData("migrate inspect . --format json")]
    [InlineData("migrate inspect --user-directory /exported/profile")]
    [InlineData("migrate inspect . --probe-mcp")]
    [InlineData("migrate inspect --probe-mcp --format json")]
    [InlineData("scan")]
    [InlineData("scan ./samples/valid-skill")]
    [InlineData("scan ./samples --strict --format sarif --output artifacts/scan.sarif")]
    [InlineData("scan ./samples --suppress SF1005")]
    [InlineData("inventory")]
    [InlineData("inventory . --probe-mcp --format json")]
    [InlineData("mcp inspect ./mcp.json")]
    [InlineData("mcp inspect ./mcp.json --probe-mcp")]
    [InlineData("mcp validate ./mcp.json --format json")]
    [InlineData("mcp diff ./old/mcp.json ./new/mcp.json")]
    [InlineData("mcp diff ./old/mcp.json ./new/mcp.json --fail-on-change")]
    [InlineData("policy check")]
    [InlineData("policy check ./samples")]
    [InlineData("policy check ./samples --policy .skillforge/policy.yaml")]
    [InlineData("policy check ./samples --format sarif --output artifacts/policy.sarif")]
    [InlineData("diff ./before ./after --format sarif")]
    [InlineData("eval ./samples/dotnet-api-review")]
    [InlineData("eval ./samples/dotnet-api-review --model qwen3:8b --model-endpoint http://localhost:11434/v1")]
    [InlineData("eval . --model gpt-5 --model-endpoint https://api.openai.com/v1 --model-api-key-env OPENAI_API_KEY")]
    [InlineData("eval . --model m --model-endpoint http://e/v1 --max-model-requests 20")]
    [InlineData("provenance")]
    [InlineData("provenance ./skills")]
    [InlineData("provenance . --format json --output artifacts/provenance.json")]
    [InlineData("provenance diff ./before ./after")]
    [InlineData("provenance diff ./before ./after --fail-on-drift --format sarif")]
    [InlineData("update analyze ./plugin-v1 ./plugin-v2")]
    [InlineData("update analyze ./plugin-v1 ./plugin-v2 --fail-on-expansion --format json")]
    [InlineData("identity inspect ./mcp.json")]
    [InlineData("identity inspect ./mcp.json --probe --format json")]
    [InlineData("identity diff ./old/mcp.json ./new/mcp.json")]
    [InlineData("identity diff ./old/mcp.json ./new/mcp.json --probe --fail-on-drift --format sarif")]
    [InlineData("mcp surface ./mcp.json")]
    [InlineData("mcp surface ./mcp.json --probe --policy .skillforge/policy.yaml")]
    [InlineData("mcp surface ./mcp.json --probe --fail-on-threshold --format json")]
    [InlineData("graph")]
    [InlineData("graph .")]
    [InlineData("graph ./repo --format json")]
    [InlineData("graph . --format mermaid --output docs/wiring.mmd")]
    [InlineData("discover")]
    [InlineData("discover postgres --registry https://registry.example.test/resources")]
    [InlineData("discover postgres --registry https://r.test/x --kind mcp-registry")]
    [InlineData("discover postgres --registry https://r.test/x --type mcpserver --limit 10 --timeout 5")]
    [InlineData("discover postgres --registry https://r.test/x --format json --output artifacts/d.json")]
    [InlineData("discovery verify --registry https://r.test/x")]
    [InlineData("discovery verify postgres --registry https://r.test/x --probe")]
    [InlineData("discovery verify postgres --registry https://r.test/x --probe --fail-on-drift")]
    [InlineData("discovery verify postgres --registry https://r.test/x --probe --format sarif -o d.sarif")]
    public void AcceptsTheDocumentedInvocations(string commandLine)
    {
        var result = Root().Parse(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("validate --nonsense")]
    [InlineData("frobnicate")]
    [InlineData("migrate apply")]
    [InlineData("policy apply")]
    [InlineData("mcp probe ./mcp.json")]
    [InlineData("scan ./samples --nonsense")]
    [InlineData("mcp validate ./mcp.json --format sarif")]
    [InlineData("migrate inspect --format sarif")]

    // 'update' installs nothing and 'provenance' verifies nothing, so neither has a verb that suggests it does.
    [InlineData("update apply")]
    [InlineData("provenance verify ./skills")]
    [InlineData("identity rotate ./mcp.json")]
    [InlineData("provenance . --format sarif")]

    // A model named with nowhere to send it, or an endpoint with no model, would quietly probe nothing.
    [InlineData("eval . --model qwen3:8b")]
    [InlineData("eval . --model-endpoint http://localhost:11434/v1")]

    // 'graph' reads files and reaches no verdict, so it has neither a probe nor a gate. Both would be a
    // different command wearing this one's name.
    [InlineData("graph . --probe")]
    [InlineData("graph . --fail-on-drift")]
    [InlineData("graph . --format sarif")]

    // Discovery reads registries. It does not install, publish or connect to what it finds.
    [InlineData("discover postgres --registry https://r.test/x --install")]
    [InlineData("discovery install postgres")]
    [InlineData("discovery connect postgres")]
    [InlineData("discovery publish ./server.json")]
    [InlineData("discover postgres --registry https://r.test/x --format sarif")]
    [InlineData("discover postgres --registry https://r.test/x --type nonsense")]
    public void RejectsWhatItDoesNotUnderstand(string commandLine)
    {
        var result = Root().Parse(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void DiscoverAndDiscoveryAreDistinctCommandsRatherThanAPrefixCollision()
    {
        // Two command names one letter apart. System.CommandLine matches tokens exactly, so this is fine — and
        // asserting it is what would catch a future rename that made one shadow the other.
        var names = Root().Subcommands.Select(command => command.Name).ToArray();

        names.Should().Contain(["discover", "discovery", "graph"]);

        Root().Parse(["discover", "q", "--registry", "https://r.test/x"]).Errors.Should().BeEmpty();
        Root().Parse(["discovery", "verify", "--registry", "https://r.test/x"]).Errors.Should().BeEmpty();

        // 'discovery' is a group: on its own it has nothing to do, and saying so beats picking a default.
        Root().Parse(["discovery"]).Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void NoCommandOffersADefaultRegistry()
    {
        // The whole security posture of discovery, asserted where it cannot be quietly undone: an option with a
        // default value factory would make a network request nobody asked for.
        foreach (var command in Root().Subcommands.SelectMany(Descendants))
        {
            foreach (var option in command.Options.Where(option => option.Name == "--registry"))
            {
                option.HasDefaultValue.Should().BeFalse(
                    $"'{command.Name} --registry' must have no default: SkillForge never reaches a registry "
                    + "nobody named");
            }
        }
    }

    private static IEnumerable<Command> Descendants(Command command) =>
        new[] { command }.Concat(command.Subcommands.SelectMany(Descendants));

    [Fact]
    public void RunningWithNoArgumentsIsAUsageError()
    {
        // Verified against the built executable too: no arguments exits 2, --help and --version exit 0.
        Root().Parse([]).Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateDefaultsToTheCurrentDirectory()
    {
        var result = Root().Parse(["validate"]);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void EveryCommandAndOptionIsDescribed()
    {
        // Help text is the only documentation most users read.
        var root = Root();

        root.Description.Should().NotBeNullOrWhiteSpace();
        root.Subcommands.Should().AllSatisfy(command =>
            command.Description.Should().NotBeNullOrWhiteSpace());
        root.Options.Where(option => option.Name != "--help" && option.Name != "--version")
            .Should().AllSatisfy(option => option.Description.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void ExposesTheValidateCommand()
    {
        Root().Subcommands.Select(command => command.Name).Should().Contain("validate");
    }

    [Fact]
    public void TheCompositionRootActuallyResolvesWhatTheCommandsAskFor()
    {
        // An earlier version of this test only built the command tree, which resolves nothing — the CLI
        // threw on first use with a green test suite. Resolve the runner for real.
        using var services = CompositionRoot.Build();

        var runner = services.GetRequiredService<ValidateCommandRunner>();

        runner.Should().NotBeNull();
    }

    [Fact]
    public void EveryRegisteredServiceCanBeConstructed()
    {
        var act = () =>
        {
            using var services = CompositionRoot.Build();
            _ = services.GetRequiredService<ISkillLoader>();
            _ = services.GetRequiredService<ISkillValidator>();
            _ = services.GetRequiredService<IValidationReportRenderer>();
        };

        act.Should().NotThrow();
    }
}
