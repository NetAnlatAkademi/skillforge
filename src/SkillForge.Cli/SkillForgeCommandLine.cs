using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SkillForge.Application.Abstractions;
using SkillForge.Cli.Commands;

namespace SkillForge.Cli;

/// <summary>
/// Defines the command surface: what a user can type and what each option means.
/// </summary>
/// <remarks>
/// Command classes hold no business logic. They translate arguments into a call on a runner and return
/// its exit code, which is why this file has no idea what a diagnostic is.
/// </remarks>
internal static class SkillForgeCommandLine
{
    /// <summary>Builds the root command with every subcommand attached.</summary>
    /// <param name="services">Provider used to resolve command runners.</param>
    /// <returns>The configured root command.</returns>
    internal static RootCommand Build(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Recursive, so they can be written before or after the subcommand. Without this they are only
        // accepted on the root, which is not how anyone types a command line.
        var quiet = new Option<bool>("--quiet", "-q")
        {
            Description = "Print only errors and the final verdict.",
            Recursive = true,
        };

        var verbose = new Option<bool>("--verbose")
        {
            Description = "Print the suggestion attached to each finding.",
            Recursive = true,
        };

        var noColor = new Option<bool>("--no-color")
        {
            Description = "Disable coloured output, for logs and pipes.",
            Recursive = true,
        };

        var root = new RootCommand(
            "SkillForge — create, validate, inspect and package AI agent skills.")
        {
            quiet,
            verbose,
            noColor,
        };

        root.Subcommands.Add(BuildValidateCommand(services, quiet, verbose, noColor));

        return root;
    }

    private static Command BuildValidateCommand(
        IServiceProvider services,
        Option<bool> quiet,
        Option<bool> verbose,
        Option<bool> noColor)
    {
        var path = new Argument<string>("path")
        {
            Description = "Skill directory, or the path of a SKILL.md file.",
            DefaultValueFactory = _ => ".",
        };

        // An unrecognised option would otherwise be swallowed as the path argument, so a typo like
        // '--stict' would silently validate a directory called '--stict'. Reject it as a usage error.
        path.Validators.Add(result =>
        {
            var value = result.Tokens.Count > 0 ? result.Tokens[0].Value : string.Empty;
            if (value.StartsWith('-'))
            {
                result.AddError($"Unrecognized option '{value}'.");
            }
        });

        var strict = new Option<bool>("--strict")
        {
            Description = "Treat warnings as failures.",
        };

        var command = new Command("validate", "Validate a skill against the SkillForge rules.")
        {
            path,
            strict,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new ReportRenderOptions(
                Quiet: parseResult.GetValue(quiet),
                Verbose: parseResult.GetValue(verbose),
                NoColor: parseResult.GetValue(noColor) || IsColourDisabledByEnvironment());

            var runner = services.GetRequiredService<ValidateCommandRunner>();

            return await runner.RunAsync(
                parseResult.GetValue(path) ?? ".",
                parseResult.GetValue(strict),
                options,
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Honours the <c>NO_COLOR</c> convention, so CI logs are clean without anyone passing a flag.
    /// </summary>
    private static bool IsColourDisabledByEnvironment() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
}
