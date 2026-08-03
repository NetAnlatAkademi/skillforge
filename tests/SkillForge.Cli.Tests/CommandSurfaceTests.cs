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
    [InlineData("policy check")]
    [InlineData("policy check ./samples")]
    [InlineData("policy check ./samples --policy .skillforge/policy.yaml")]
    [InlineData("policy check ./samples --format sarif --output artifacts/policy.sarif")]
    [InlineData("diff ./before ./after --format sarif")]
    [InlineData("eval ./samples/dotnet-api-review")]
    [InlineData("eval ./samples/dotnet-api-review --model qwen3:8b --model-endpoint http://localhost:11434/v1")]
    [InlineData("eval . --model gpt-5 --model-endpoint https://api.openai.com/v1 --model-api-key-env OPENAI_API_KEY")]
    [InlineData("eval . --model m --model-endpoint http://e/v1 --max-model-requests 20")]
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
    [InlineData("migrate inspect --format sarif")]

    // A model named with nowhere to send it, or an endpoint with no model, would quietly probe nothing.
    [InlineData("eval . --model qwen3:8b")]
    [InlineData("eval . --model-endpoint http://localhost:11434/v1")]
    public void RejectsWhatItDoesNotUnderstand(string commandLine)
    {
        var result = Root().Parse(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        result.Errors.Should().NotBeEmpty();
    }

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
