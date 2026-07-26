using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SkillForge.Application.Abstractions;
using SkillForge.Cli.Commands;

namespace SkillForge.Cli;

/// <summary>
/// Defines the command surface: what a user can type and what each option means.
/// </summary>
/// <remarks>
/// Command classes hold no business logic. They translate arguments into a call on a runner and return its
/// exit code, which is why this file has no idea what a diagnostic is.
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

        var globals = new GlobalOptions(quiet, verbose, noColor);

        root.Subcommands.Add(BuildInitCommand(services, globals));
        root.Subcommands.Add(BuildValidateCommand(services, globals));
        root.Subcommands.Add(BuildInspectCommand(services, globals));
        root.Subcommands.Add(BuildPackCommand(services, globals));

        return root;
    }

    private static Command BuildValidateCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();

        var strict = new Option<bool>("--strict")
        {
            Description = "Treat warnings as failures.",
        };

        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var command = new Command("validate", "Validate a skill against the SkillForge rules.")
        {
            path,
            strict,
            format,
            output,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<ValidateCommandRunner>();

            return await runner.RunAsync(
                new ValidateRequest(
                    parseResult.GetValue(path) ?? ".",
                    parseResult.GetValue(strict),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildInitCommand(IServiceProvider services, GlobalOptions globals)
    {
        var name = new Argument<string>("name")
        {
            Description = "Skill name: lowercase letters, digits and single hyphens.",
        };

        var directory = new Option<string?>("--directory", "-d")
        {
            Description = "Where to create the skill. Defaults to a directory named after the skill.",
        };

        var description = new Option<string?>("--description")
        {
            Description = "Description for the frontmatter.",
        };

        var author = new Option<string?>("--author")
        {
            Description = "Author recorded in metadata.",
        };

        var license = new Option<string>("--license")
        {
            Description = "SPDX licence identifier.",
            DefaultValueFactory = _ => "MIT",
        };

        var force = new Option<bool>("--force")
        {
            Description = "Overwrite an existing skill in the target directory.",
        };

        var command = new Command("init", "Create a new skill from a template.")
        {
            name,
            directory,
            description,
            author,
            license,
            force,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<InitCommandRunner>();

            return await runner.RunAsync(
                new InitRequest(
                    parseResult.GetValue(directory),
                    new SkillInitializationOptions(
                        parseResult.GetValue(name) ?? string.Empty,
                        parseResult.GetValue(description),
                        parseResult.GetValue(author),
                        parseResult.GetValue(license) ?? "MIT",
                        Force: parseResult.GetValue(force)),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildInspectCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();
        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var showFiles = new Option<bool>("--show-files") { Description = "List every file in the skill." };
        var showLinks = new Option<bool>("--show-links") { Description = "List external URLs." };
        var showPermissions = new Option<bool>("--show-permissions")
        {
            Description = "List the capabilities the skill's contents imply.",
        };

        var command = new Command("inspect", "Summarise a skill's contents and behaviour surface.")
        {
            path,
            format,
            output,
            showFiles,
            showLinks,
            showPermissions,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<InspectCommandRunner>();

            return await runner.RunAsync(
                new InspectRequest(
                    parseResult.GetValue(path) ?? ".",
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    parseResult.GetValue(showFiles),
                    parseResult.GetValue(showLinks),
                    parseResult.GetValue(showPermissions),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildPackCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();

        var output = new Option<string>("--output", "-o")
        {
            Description = "Directory to write the package to.",
            DefaultValueFactory = _ => "artifacts",
        };

        var version = new Option<string?>("--version-override")
        {
            Description = "Version to package as, overriding the skill's own metadata.",
        };

        var skipValidation = new Option<bool>("--skip-validation")
        {
            Description = "Package even if validation finds errors. Use deliberately.",
        };

        var command = new Command("pack", "Package a skill into a deterministic archive.")
        {
            path,
            output,
            version,
            skipValidation,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<PackCommandRunner>();

            return await runner.RunAsync(
                new PackRequest(
                    parseResult.GetValue(path) ?? ".",
                    parseResult.GetValue(output) ?? "artifacts",
                    parseResult.GetValue(version),
                    parseResult.GetValue(skipValidation),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Argument<string> CreateSkillPathArgument()
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

        return path;
    }

    private static Option<string> CreateFormatOption(params string[] allowed)
    {
        var accepted = allowed.Length == 0 ? OutputFormat.All : allowed;

        var format = new Option<string>("--format", "-f")
        {
            Description = $"Output format: {string.Join(", ", accepted)}.",
            DefaultValueFactory = _ => OutputFormat.Console,
        };

        format.Validators.Add(result =>
        {
            var value = result.Tokens.Count > 0 ? result.Tokens[0].Value : OutputFormat.Console;
            if (!accepted.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                result.AddError(
                    $"'{value}' is not a supported format. Use one of: {string.Join(", ", accepted)}.");
            }
        });

        return format;
    }

    private static Option<string?> CreateOutputOption() =>
        new("--output", "-o")
        {
            Description = "Write machine-readable output to this file instead of stdout.",
        };

    /// <summary>
    /// The options every command shares, read back out of a parse result in one place.
    /// </summary>
    private sealed record GlobalOptions(Option<bool> Quiet, Option<bool> Verbose, Option<bool> NoColor)
    {
        internal ReportRenderOptions Read(ParseResult parseResult) =>
            new(
                Quiet: parseResult.GetValue(Quiet),
                Verbose: parseResult.GetValue(Verbose),
                NoColor: parseResult.GetValue(NoColor) || IsColourDisabledByEnvironment());

        /// <summary>
        /// Honours the <c>NO_COLOR</c> convention, so CI logs are clean without anyone passing a flag.
        /// </summary>
        private static bool IsColourDisabledByEnvironment() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }
}
