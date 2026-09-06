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
        root.Subcommands.Add(BuildScanCommand(services, globals));
        root.Subcommands.Add(BuildInventoryCommand(services, globals));
        root.Subcommands.Add(BuildMcpCommand(services, globals));
        root.Subcommands.Add(BuildProvenanceCommand(services, globals));
        root.Subcommands.Add(BuildUpdateCommand(services, globals));
        root.Subcommands.Add(BuildIdentityCommand(services, globals));

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
    /// <summary>
    /// Builds <c>scan</c>: <c>validate</c>'s rules, reported down to the risk signals.
    /// </summary>
    /// <remarks>
    /// Not a second engine. There is one rule pipeline, and a scanner with its own would be a second set of bugs and
    /// a second set of measurements. What <c>scan</c> changes is the report — see <c>RiskSignalCodes</c> for the list
    /// and the reason a missing license is not on it.
    /// </remarks>
    private static Command BuildScanCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();
        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var strict = new Option<bool>("--strict")
        {
            Description = "Treat warnings as failures.",
        };

        var suppress = new Option<string[]>("--suppress")
        {
            Description = "Diagnostic codes not to report, comma-separated or repeated.",
            AllowMultipleArgumentsPerToken = true,
        };

        var command = new Command(
            "scan",
            "Report a skill's risk signals: what it runs, what it reaches, and what its text asks an agent to do.")
        {
            path,
            strict,
            format,
            output,
            suppress,
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
                    [])
                {
                    RiskSignalsOnly = true,
                },
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Builds <c>inventory</c>, which is <c>migrate inspect</c> under the name the work plan uses for it. One runner,
    /// two entry points: the inventory is useful on its own, and not only to somebody moving between tools.
    /// </summary>
    private static Command BuildInventoryCommand(IServiceProvider services, GlobalOptions globals) =>
        BuildMigrateInspectCommand(services, globals, "inventory");

    /// <summary>
    /// Builds the <c>mcp</c> group: the same checks <c>migrate inspect</c> makes, against a file the caller names
    /// rather than the files a provider owns. That is the difference between "what is on this machine" and "what
    /// does this pull request declare".
    /// </summary>
    private static Command BuildMcpCommand(IServiceProvider services, GlobalOptions globals)
    {
        return new Command("mcp", "Inspect, validate and compare MCP configuration files.")
        {
            BuildMcpInspectionCommand(
                services,
                globals,
                "inspect",
                "Report what an MCP configuration file declares.",
                gate: false),
            BuildMcpInspectionCommand(
                services,
                globals,
                "validate",
                "Report what an MCP configuration file declares, and fail when there is anything to report.",
                gate: true),
            BuildMcpDiffCommand(services, globals),
            BuildMcpSurfaceCommand(services, globals),
        };
    }

    /// <summary>
    /// Builds <c>mcp surface</c>: how many tools a server puts in an agent's context before it has done anything.
    /// </summary>
    /// <remarks>
    /// <c>--probe</c> is not optional in practice and is still opt-in in principle. A tool list comes from the
    /// server, so without it the report says so per server rather than printing zeroes — a count of nothing must
    /// never read as "there is nothing there".
    /// </remarks>
    private static Command BuildMcpSurfaceCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = new Argument<string>("file")
        {
            Description = "MCP configuration file to read.",
        };

        var probe = CreateProbeOption();

        var policy = new Option<string>("--policy")
        {
            Description = "Policy file to read 'mcp.surface' thresholds from. Without one, the documented "
                + "defaults apply and the report says they are defaults.",
            DefaultValueFactory = _ => DefaultPolicyPath,
        };

        var failOnThreshold = new Option<bool>("--fail-on-threshold")
        {
            Description = "Fail when a server exposes at least as many tools as the warning threshold.",
        };

        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var surface = new Command(
            "surface",
            "Measure how many tools each MCP server exposes, and how many of them change things.")
        {
            path,
            probe,
            policy,
            failOnThreshold,
            format,
            output,
        };

        surface.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<McpCommandRunner>();

            return await runner.SurfaceAsync(
                new McpSurfaceRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(probe),
                    parseResult.GetValue(policy) ?? DefaultPolicyPath,
                    parseResult.GetValue(failOnThreshold),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return surface;
    }

    private static Command BuildMcpInspectionCommand(
        IServiceProvider services,
        GlobalOptions globals,
        string name,
        string description,
        bool gate)
    {
        var path = new Argument<string>("path")
        {
            Description = "MCP configuration file: .json or .toml.",
        };

        var probeMcp = new Option<bool>("--probe-mcp")
        {
            Description = "Ask each HTTP server about itself with one server/discover request. Local stdio "
                + "servers are never launched.",
        };

        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var command = new Command(name, description) { path, probeMcp, format, output };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<McpCommandRunner>();

            return await runner.InspectAsync(
                new McpRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(probeMcp),
                    gate,
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildMcpDiffCommand(IServiceProvider services, GlobalOptions globals)
    {
        var before = new Argument<string>("before") { Description = "The earlier configuration file." };
        var after = new Argument<string>("after") { Description = "The later configuration file." };

        var failOnChange = new Option<bool>("--fail-on-change")
        {
            Description = "Fail on any change, not only on a file that could not be read.",
        };

        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var command = new Command(
            "diff",
            "Compare two MCP configuration files by what they would connect to.")
        {
            before,
            after,
            failOnChange,
            format,
            output,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<McpCommandRunner>();

            return await runner.DiffAsync(
                new McpDiffRequest(
                    parseResult.GetValue(before) ?? DefaultPath,
                    parseResult.GetValue(after) ?? DefaultPath,
                    parseResult.GetValue(failOnChange),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildMigrateCommand(IServiceProvider services, GlobalOptions globals) =>
        new("migrate", "Inspect agent tooling across providers, ahead of moving between them.")
        {
            BuildMigrateInspectCommand(services, globals, "inspect"),
        };

    /// <summary>
    /// Builds the inventory command. Called twice — once as <c>migrate inspect</c>, once as <c>inventory</c> — and it
    /// builds a fresh instance each time, because a command belongs to one parent.
    /// </summary>
    private static Command BuildMigrateInspectCommand(
        IServiceProvider services,
        GlobalOptions globals,
        string name)
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
            name,
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

        return inspect;
    }

    /// <summary>
    /// Builds <c>policy check</c> and <c>policy diff</c>. A subcommand from the start, because a policy is a thing
    /// an organisation will want to explain and list as well as enforce.
    /// </summary>
    private static Command BuildPolicyCommand(IServiceProvider services, GlobalOptions globals)
    {
        return new Command("policy", "Apply an organisation's policy, and see what changed in it.")
        {
            BuildPolicyCheckCommand(services, globals),
            BuildPolicyDiffCommand(services, globals),
        };
    }

    private static Command BuildPolicyCheckCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = CreateSkillPathArgument();

        var policy = new Option<string>("--policy")
        {
            Description = "Policy file to judge the skills against.",
            DefaultValueFactory = _ => DefaultPolicyPath,
        };

        var mcp = new Option<string[]>("--mcp")
        {
            Description = "MCP configuration files to judge against the policy's allow and deny rules, "
                + "comma-separated or repeated. Without one, those rules report themselves as SF9009.",
            AllowMultipleArgumentsPerToken = true,
        };

        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var check = new Command("check", "Judge skills against the organisation's policy.")
        {
            path,
            policy,
            mcp,
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
                    ReadPaths(parseResult.GetValue(mcp)),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return check;
    }

    private static Command BuildPolicyDiffCommand(IServiceProvider services, GlobalOptions globals)
    {
        var before = new Argument<string>("before")
        {
            Description = "The earlier policy file.",
        };

        var after = new Argument<string>("after")
        {
            Description = "The later policy file.",
        };

        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var failOnWeakening = new Option<bool>("--fail-on-weakening")
        {
            Description = "Fail when the later policy permits something the earlier one did not.",
        };

        var diff = new Command("diff", "Compare two policy files by what they decide, and report what was relaxed.")
        {
            before,
            after,
            format,
            output,
            failOnWeakening,
        };

        diff.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<PolicyDiffCommandRunner>();

            return await runner.RunAsync(
                new PolicyDiffRequest(
                    parseResult.GetValue(before) ?? DefaultPolicyPath,
                    parseResult.GetValue(after) ?? DefaultPolicyPath,
                    parseResult.GetValue(failOnWeakening),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return diff;
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

    /// <summary>
    /// Builds <c>provenance</c>, with <c>provenance diff</c> under it.
    /// </summary>
    /// <remarks>
    /// A group from the start, for the reason <c>policy</c> is one: where an asset came from and how that changed
    /// are the same question asked of one tree and of two, and splitting them across unrelated top-level commands
    /// would hide the second behind the first.
    /// </remarks>
    private static Command BuildProvenanceCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = new Argument<string>("path")
        {
            Description = "Directory to trace. Defaults to the current directory.",
            DefaultValueFactory = _ => DefaultPath,
        };

        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var command = new Command(
            "provenance",
            "Report where each skill and plugin came from, and how it gets its next version.")
        {
            path,
            format,
            output,
            BuildProvenanceDiffCommand(services, globals),
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<ProvenanceCommandRunner>();

            return await runner.RunAsync(
                new ProvenanceRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Builds <c>identity</c>, with <c>inspect</c> and <c>diff</c> under it.
    /// </summary>
    /// <remarks>
    /// Its own group rather than a subcommand of <c>mcp</c>: the question is whose authority an agent is using,
    /// and the answer will eventually come from more places than an MCP configuration file.
    /// </remarks>
    private static Command BuildIdentityCommand(IServiceProvider services, GlobalOptions globals) =>
        new("identity", "Report the identity an agent reaches each MCP server with, and how it changed.")
        {
            BuildIdentityInspectCommand(services, globals),
            BuildIdentityDiffCommand(services, globals),
        };

    private static Command BuildIdentityInspectCommand(IServiceProvider services, GlobalOptions globals)
    {
        var path = new Argument<string>("file")
        {
            Description = "MCP configuration file to read.",
        };

        var probe = CreateProbeOption();
        var format = CreateFormatOption(OutputFormat.Console, OutputFormat.Json);
        var output = CreateOutputOption();

        var inspect = new Command(
            "inspect",
            "Report what identity each declared MCP server is reached with, from names only.")
        {
            path,
            probe,
            format,
            output,
        };

        inspect.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<IdentityCommandRunner>();

            return await runner.InspectAsync(
                new IdentityInspectRequest(
                    parseResult.GetValue(path) ?? DefaultPath,
                    parseResult.GetValue(probe),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return inspect;
    }

    private static Command BuildIdentityDiffCommand(IServiceProvider services, GlobalOptions globals)
    {
        var before = new Argument<string>("before")
        {
            Description = "The earlier MCP configuration file.",
        };

        var after = new Argument<string>("after")
        {
            Description = "The later MCP configuration file.",
        };

        var probe = CreateProbeOption();
        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var failOnDrift = new Option<bool>("--fail-on-drift")
        {
            Description = "Fail when an identity widened: a new scope, a new delegation, a longer-lived credential.",
        };

        var diff = new Command(
            "diff",
            "Compare two MCP configurations by the identity each server is reached with.")
        {
            before,
            after,
            probe,
            format,
            output,
            failOnDrift,
        };

        diff.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<IdentityCommandRunner>();

            return await runner.DiffAsync(
                new IdentityDiffRequest(
                    parseResult.GetValue(before) ?? DefaultPath,
                    parseResult.GetValue(after) ?? DefaultPath,
                    parseResult.GetValue(probe),
                    parseResult.GetValue(failOnDrift),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return diff;
    }

    /// <summary>
    /// The flag that lets a command speak to a server. Shared, and worded the same everywhere, because "does this
    /// leave my machine" is a question a user should never have to answer twice.
    /// </summary>
    private static Option<bool> CreateProbeOption() =>
        new("--probe")
        {
            Description = "Ask each HTTP MCP server about itself with one request. Local stdio servers are never "
                + "launched.",
        };

    /// <summary>
    /// Builds <c>update</c>, with <c>update analyze</c> under it.
    /// </summary>
    /// <remarks>
    /// A group with one subcommand today, because "what would this update do" is a question that will grow other
    /// verbs, and because <c>skillforge update</c> on its own must never read as a command that installs anything.
    /// </remarks>
    private static Command BuildUpdateCommand(IServiceProvider services, GlobalOptions globals) =>
        new("update", "Analyse what an update does before it arrives.")
        {
            BuildUpdateAnalyzeCommand(services, globals),
        };

    private static Command BuildUpdateAnalyzeCommand(IServiceProvider services, GlobalOptions globals)
    {
        var baseTree = new Argument<string>("base")
        {
            Description = "The version the update starts from: a plugin or skill directory.",
        };

        var target = new Argument<string>("target")
        {
            Description = "The version it arrives at.",
        };

        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var failOnExpansion = new Option<bool>("--fail-on-expansion")
        {
            Description = "Fail when the update lets the asset reach further than what it replaces.",
        };

        var analyze = new Command(
            "analyze",
            "Report what an update adds — skills, MCP servers, hooks, scripts, hosts, credentials — and what "
                + "that means given how it arrives.")
        {
            baseTree,
            target,
            format,
            output,
            failOnExpansion,
        };

        analyze.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<UpdateAnalyzeCommandRunner>();

            return await runner.RunAsync(
                new UpdateAnalyzeRequest(
                    parseResult.GetValue(baseTree) ?? DefaultPath,
                    parseResult.GetValue(target) ?? DefaultPath,
                    parseResult.GetValue(failOnExpansion),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return analyze;
    }

    private static Command BuildProvenanceDiffCommand(IServiceProvider services, GlobalOptions globals)
    {
        var before = new Argument<string>("before")
        {
            Description = "The earlier tree.",
        };

        var after = new Argument<string>("after")
        {
            Description = "The later tree.",
        };

        var format = CreateFormatOption();
        var output = CreateOutputOption();

        var failOnDrift = new Option<bool>("--fail-on-drift")
        {
            Description = "Fail when something drifted: a changed publisher, marketplace, pin or fingerprint.",
        };

        var diff = new Command(
            "diff",
            "Compare two trees by where their assets come from, and report what drifted.")
        {
            before,
            after,
            format,
            output,
            failOnDrift,
        };

        diff.SetAction(async (parseResult, cancellationToken) =>
        {
            var runner = services.GetRequiredService<ProvenanceCommandRunner>();

            return await runner.DiffAsync(
                new ProvenanceDiffRequest(
                    parseResult.GetValue(before) ?? DefaultPath,
                    parseResult.GetValue(after) ?? DefaultPath,
                    parseResult.GetValue(failOnDrift),
                    parseResult.GetValue(format) ?? OutputFormat.Console,
                    parseResult.GetValue(output),
                    globals.Read(parseResult)),
                cancellationToken).ConfigureAwait(false);
        });

        return diff;
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

    /// <summary>
    /// Reads a repeated path option. Split on commas like the others, which is safe here because a comma is not a
    /// path separator on any platform SkillForge runs on.
    /// </summary>
    private static string[] ReadPaths(string[]? tokens) =>
        tokens is null ? [] : [.. tokens.SelectMany(SplitCodes)];

    [GeneratedRegex("^SF[0-8][0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCodePattern();

    private static Option<string?> CreateOutputOption() =>
        new("--output", "-o")
        {
            Description = "Write machine-readable output to this file instead of stdout.",
        };
}
