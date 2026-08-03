using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SkillForge.Application.Abstractions;
using SkillForge.Cli.Commands;
using SkillForge.Domain.Modeling;

namespace SkillForge.Cli;

/// <summary>
/// Defines the command surface: what a user can type and what each option means.
/// </summary>
/// <remarks>
/// Command classes hold no business logic. They translate arguments into a call on a runner and return its
/// exit code, which is why this file has no idea what a diagnostic is.
/// </remarks>
internal static partial class SkillForgeCommandLine
{
    private const string DefaultPath = ".";
    private const string DefaultLicense = "MIT";
    private const string DefaultOutputDirectory = "artifacts";
    private const string DefaultPolicyPath = ".skillforge/policy.yaml";

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
        root.Subcommands.Add(BuildEvalCommand(services, globals));
        root.Subcommands.Add(BuildDiffCommand(services, globals));
        root.Subcommands.Add(BuildPackCommand(services, globals));
        root.Subcommands.Add(BuildMigrateCommand(services, globals));
        root.Subcommands.Add(BuildPolicyCommand(services, globals));

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

        var suppress = new Option<string[]>("--suppress")
        {
            Description = "Diagnostic codes not to report, comma-separated or repeated (e.g. SF1009,SF1010). "
                + "Suppressed findings are counted and the count is always shown.",
            AllowMultipleArgumentsPerToken = true,
        };

        // A typo here would otherwise suppress nothing and say nothing, which is the worst outcome for a flag
        // whose whole job is to remove output.
        suppress.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
            {
                foreach (var code in SplitCodes(token.Value))
                {
                    if (!DiagnosticCodePattern().IsMatch(code))
                    {
                        result.AddError($"'{code}' is not a diagnostic code. Codes look like SF1009.");
                    }
                }
            }
        });

        var provider = new Option<string[]>("--provider")
        {
            Description = "Also check the skill against these agent providers, comma-separated or repeated "
                + "(e.g. claude-code,codex), even when it does not declare them.",
            AllowMultipleArgumentsPerToken = true,
        };

        var command = new Command("validate", "Validate a skill against the SkillForge rules.")
        {
            path,
            strict,
            format,
            output,
            suppress,
            provider,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<ValidateCommandRunner>();

            return await runner.RunAsync(
                new ValidateRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(strict),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult),
                    ReadSuppressedCodes(parseResult.GetValue(suppress)),
                    ReadProviders(parseResult.GetValue(provider))),
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
            DefaultValueFactory = _ => DefaultLicense,
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
                        parseResult.GetValue(license) ?? DefaultLicense,
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
                    parseResult.GetValue(path) ?? DefaultPath,
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

    /// <summary>
    /// Builds <c>migrate</c>, which is a group rather than a command of its own.
    /// </summary>
    /// <remarks>
    /// <c>inspect</c> is the only thing under it today and it would have been shorter as <c>migrate-inspect</c>.
    /// The group is deliberate: reading a setup and changing one are different acts with different risks, and a
    /// later <c>migrate apply</c> must not be reachable by a typo in a flag. Naming the read explicitly keeps the
    /// write in its own place.
    /// </remarks>
    private static Command BuildMigrateCommand(IServiceProvider services, GlobalOptions globals)
    {
        var project = new Argument<string?>("project")
        {
            Description = "Project directory to include project-scoped configuration from. Optional; without it "
                + "only user-scoped configuration is read.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var userDirectory = new Option<string?>("--user-directory")
        {
            Description = "Read this directory instead of the current user's home directory.",
        };

        // The only part of this command that leaves the machine, so it is a flag rather than a default. stdio servers
        // are never launched even with it: inspecting a server by running it would argue with the whole product.
        var probeMcp = new Option<bool>("--probe-mcp")
        {
            Description = "Ask each HTTP MCP server about itself with one server/discover request. Local stdio "
                + "servers are never launched.",
        };

        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var inspect = new Command(
            "inspect",
            "Report the installed agent tooling: skills, MCP servers and instruction files, per provider.")
        {
            project,
            userDirectory,
            probeMcp,
            format,
            output,
        };

        inspect.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<MigrateInspectCommandRunner>();

            return await runner.RunAsync(
                new MigrateInspectRequest(
                    parseResult.GetValue(project),
                    parseResult.GetValue(userDirectory),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult),
                    parseResult.GetValue(probeMcp)),
                cancellationToken).ConfigureAwait(false);
        });

        return new Command("migrate", "Inspect agent tooling across providers, ahead of moving between them.")
        {
            inspect,
        };
    }

    /// <summary>
    /// Builds <c>policy check</c>. A subcommand from the start, because a policy is a thing an organisation will
    /// want to explain and list as well as enforce, and <c>policy check</c> leaves room for that.
    /// </summary>
    private static Command BuildPolicyCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();

        var policy = new Option<string>("--policy")
        {
            Description = "Policy file to judge the skills against.",
            DefaultValueFactory = _ => DefaultPolicyPath,
        };

        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var check = new Command("check", "Judge skills against the organisation's policy.")
        {
            path,
            policy,
            format,
            output,
        };

        check.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<PolicyCheckCommandRunner>();

            return await runner.RunAsync(
                new PolicyCheckRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(policy) ?? DefaultPolicyPath,
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return new Command("policy", "Apply an organisation's policy to its skills.")
        {
            check,
        };
    }

    private static Command BuildEvalCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();
        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        // Model options. Two flags rather than one, and no default endpoint anywhere: SkillForge sends nothing to
        // anything unless the person running it says where to send it, and a default would make that decision for them.
        var model = new Option<string?>("--model")
        {
            Description = "Model to ask for model_activation cases, e.g. qwen3:8b or gpt-5. Requires --model-endpoint.",
        };

        var modelEndpoint = new Option<string?>("--model-endpoint")
        {
            Description = "Base URL of an OpenAI-compatible API, e.g. http://localhost:11434/v1 for Ollama.",
        };

        var modelApiKeyEnv = new Option<string?>("--model-api-key-env")
        {
            Description = "Name of the environment variable holding the API key. The key itself is never read from "
                + "an argument, so it cannot end up in a shell history or a CI log.",
        };

        var maxModelRequests = new Option<int>("--max-model-requests")
        {
            Description = "Refuse to make more than this many model requests in one run.",
            DefaultValueFactory = _ => EvalRequest.DefaultMaxModelRequests,
        };

        var command = new Command("eval", "Check a skill against the expectations declared under evals/.")
        {
            path,
            format,
            output,
            model,
            modelEndpoint,
            modelApiKeyEnv,
            maxModelRequests,
        };

        // One without the other is a mistake worth catching at parse time: --model alone would otherwise look like it
        // worked and quietly probe nothing.
        command.Validators.Add(result =>
        {
            var hasModel = result.GetValue(model) is { Length: > 0 };
            var hasEndpoint = result.GetValue(modelEndpoint) is { Length: > 0 };

            if (hasModel != hasEndpoint)
            {
                result.AddError("--model and --model-endpoint go together: name the model and say where it lives.");
            }
        });

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<EvalCommandRunner>();

            return await runner.RunAsync(
                new EvalRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult),
                    ReadModelSettings(
                        parseResult.GetValue(model),
                        parseResult.GetValue(modelEndpoint),
                        parseResult.GetValue(modelApiKeyEnv)),
                    parseResult.GetValue(maxModelRequests)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildDiffCommand(IServiceProvider services, GlobalOptions globals)
    {
        var before = new Argument<string>("before")
        {
            Description = "The earlier version: a skill directory, or the path of a SKILL.md file.",
        };

        var after = new Argument<string>("after")
        {
            Description = "The later version.",
        };

        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json, OutputFormat.Sarif);
        var output = CreateOutputOption();

        var failOnChange = new Option<bool>("--fail-on-change")
        {
            Description = "Fail on any surface change, not only on a new error.",
        };

        var command = new Command(
            "diff",
            "Compare two versions of a skill by what they can do, not by which bytes changed.")
        {
            before,
            after,
            format,
            output,
            failOnChange,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<DiffCommandRunner>();

            return await runner.RunAsync(
                new DiffRequest(
                    parseResult.GetValue(before) ?? DefaultPath,
                    parseResult.GetValue(after) ?? DefaultPath,
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    parseResult.GetValue(failOnChange),
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
            DefaultValueFactory = _ => DefaultOutputDirectory,
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
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(output) ?? DefaultOutputDirectory,
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
            DefaultValueFactory = _ => DefaultPath,
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

    /// <summary>
    /// Accepts both <c>--suppress SF1009,SF1010</c> and a repeated <c>--suppress</c>, because people reasonably
    /// expect either to work.
    /// </summary>
    private static string[] ReadSuppressedCodes(string[]? tokens) =>
        tokens is null ? [] : [.. tokens.SelectMany(SplitCodes)];

    /// <summary>
    /// Builds the model settings, or <see langword="null"/> when the caller named no model — which is what keeps every
    /// other run of every other command entirely offline.
    /// </summary>
    private static ModelSettings? ReadModelSettings(string? model, string? endpoint, string? apiKeyEnvironment) =>
        model is { Length: > 0 } && endpoint is { Length: > 0 }
            ? new ModelSettings(endpoint, model, apiKeyEnvironment)
            : null;

    private static IEnumerable<string> SplitCodes(string token) =>
        token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Reads <c>--provider</c> the same way as <c>--suppress</c>. Unlike a diagnostic code, an unrecognised
    /// provider identifier is not rejected here: SF7001 reports it as a finding, which is more use than a usage
    /// error, because the identifier may be a real provider SkillForge has not learned yet.
    /// </summary>
    private static string[] ReadProviders(string[]? tokens) =>
        tokens is null ? [] : [.. tokens.SelectMany(SplitCodes)];

    [GeneratedRegex("^SF[0-8][0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCodePattern();

    private static Option<string?> CreateOutputOption() =>
        new("--output", "-o")
        {
            Description = "Write machine-readable output to this file instead of stdout.",
        };
}
