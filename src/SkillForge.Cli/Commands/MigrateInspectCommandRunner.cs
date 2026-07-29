using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Migration;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge migrate inspect</c> does.
/// </summary>
/// <remarks>
/// It describes, and it does not judge — the same stance as <c>inspect</c> (ADR-006). It reports what is installed,
/// what each tool declares and which instruction files are in play, and it exits zero even when it lists twenty
/// skills and three MCP servers. Deciding that two instruction files contradict each other, or that a server
/// duplicates another, is reading somebody's setup for meaning, and this release does not claim to.
///
/// It never prints an environment variable's value; see <see cref="McpServerDeclaration"/>.
/// </remarks>
internal sealed class MigrateInspectCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IMigrationInspector _inspector;
    private readonly IUserEnvironment _userEnvironment;
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the runner.</summary>
    /// <param name="inspector">Runs the provider adapters.</param>
    /// <param name="userEnvironment">Supplies the home directory when the caller does not name one.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    public MigrateInspectCommandRunner(
        IMigrationInspector inspector,
        IUserEnvironment userEnvironment,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(userEnvironment);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _inspector = inspector;
        _userEnvironment = userEnvironment;
        _fileSystem = fileSystem;
    }

    /// <summary>Reads the installed agent tooling and prints the inventory.</summary>
    /// <param name="request">What to inspect and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns><see cref="ExitCodes.Success"/>; an inventory has nothing to fail at.</returns>
    internal async Task<int> RunAsync(MigrateInspectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inspection = await _inspector
            .InspectAsync(
                new AgentToolScanRequest(
                    _fileSystem.GetFullPath(request.UserDirectory ?? _userEnvironment.HomeDirectory),
                    request.ProjectPath is { Length: > 0 } project
                        ? _fileSystem.GetFullPath(project)
                        : null),
                request.ProbeMcpServers,
                cancellationToken)
            .ConfigureAwait(false);

        var text = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(inspection)
            : ToText(inspection);

        if (request.OutputPath is { Length: > 0 } outputPath)
        {
            var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
            if (directory is { Length: > 0 })
            {
                _fileSystem.CreateDirectory(directory);
            }

            await _fileSystem.WriteAllTextAsync(outputPath, text, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Console.Out.WriteAsync(text).ConfigureAwait(false);
        }

        return ExitCodes.Success;
    }

    private static string ToText(MigrationInspection inspection)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Migrate Inspect");
        builder.AppendLine();
        builder.AppendLine($"User:    {inspection.UserDirectory}");
        builder.AppendLine($"Project: {inspection.ProjectDirectory ?? "(not inspected)"}");

        AppendProviders(builder, inspection);
        AppendSkills(builder, inspection);
        AppendMcpServers(builder, inspection);
        AppendInstructionFiles(builder, inspection);
        AppendMcpProbes(builder, inspection);
        AppendDiagnostics(builder, inspection);

        builder.AppendLine();
        builder.AppendLine(
            "This is an inventory, not a verdict. It reports what is installed and what each tool declares; "
                + "run 'validate' to judge a skill.");
        builder.AppendLine(
            "Environment variable values are never read or printed — only the names a declaration sets.");

        return builder.ToString();
    }

    private static void AppendProviders(StringBuilder builder, MigrationInspection inspection)
    {
        builder.AppendLine();
        builder.AppendLine("Providers:");

        foreach (var provider in inspection.Providers)
        {
            builder.AppendLine(provider.IsPresent
                ? $"  {provider.DisplayName} ({provider.ProviderId}) — found"
                : $"  {provider.DisplayName} ({provider.ProviderId}) — not found");

            foreach (var path in provider.ConfigurationPaths)
            {
                builder.AppendLine($"      {path}");
            }
        }
    }

    private static void AppendSkills(StringBuilder builder, MigrationInspection inspection)
    {
        builder.AppendLine();
        builder.AppendLine($"Skills ({inspection.Skills.Count}):");

        if (inspection.Skills.Count == 0)
        {
            builder.AppendLine("  (none found)");
            return;
        }

        foreach (var group in inspection.Skills.GroupBy(skill => skill.ProviderId))
        {
            builder.AppendLine($"  {group.Key}:");

            foreach (var skill in group)
            {
                // The declared compatibility is shown because a skill installed for one provider while naming
                // another is the observation a migration turns on.
                var declared = skill.DeclaredCompatibility.Count == 0
                    ? "declares no compatibility"
                    : $"declares {string.Join(", ", skill.DeclaredCompatibility)}";

                builder.AppendLine($"      {skill.Name} ({declared})");
            }
        }
    }

    private static void AppendMcpServers(StringBuilder builder, MigrationInspection inspection)
    {
        builder.AppendLine();
        builder.AppendLine($"MCP servers ({inspection.McpServers.Count}):");

        if (inspection.McpServers.Count == 0)
        {
            builder.AppendLine("  (none declared)");
            return;
        }

        foreach (var group in inspection.McpServers.GroupBy(server => server.ProviderId))
        {
            builder.AppendLine($"  {group.Key}:");

            foreach (var server in group)
            {
                builder.AppendLine($"      {server.Name} [{server.Transport}] {server.Command ?? "(no command)"}");

                if (server.EnvironmentVariableNames.Count > 0)
                {
                    builder.AppendLine(
                        $"          env: {string.Join(", ", server.EnvironmentVariableNames)} (names only)");
                }
            }
        }
    }

    private static void AppendInstructionFiles(StringBuilder builder, MigrationInspection inspection)
    {
        builder.AppendLine();
        builder.AppendLine($"Instruction files ({inspection.InstructionFiles.Count}):");

        if (inspection.InstructionFiles.Count == 0)
        {
            builder.AppendLine("  (none found)");
            return;
        }

        foreach (var file in inspection.InstructionFiles)
        {
            builder.AppendLine(
                $"  [{file.Scope}] {file.Path} ({file.SizeInBytes} bytes, read by {file.ProviderId})");
        }

        // Whether these agree with each other is a judgement about prose. Naming them, and how much reading each
        // one is, is the part SkillForge can do honestly.
        if (inspection.InstructionFiles.Count > 1)
        {
            builder.AppendLine();
            builder.AppendLine(
                "  More than one instruction file is in play. SkillForge does not judge whether they agree — "
                    + "that is a reading of your prose, not a fact it can compute.");
        }
    }

    /// <summary>
    /// Writes what each probed server said about itself.
    /// </summary>
    /// <remarks>
    /// The self-reported name and version are labelled as such every time they appear. The specification is explicit
    /// that <c>serverInfo</c> is not verified by the protocol and that clients should not use it for security
    /// decisions, so presenting it as plain fact would be repeating a claim as if SkillForge had checked it.
    /// </remarks>
    private static void AppendMcpProbes(StringBuilder builder, MigrationInspection inspection)
    {
        if (inspection.McpProbes.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("MCP protocol probe:");

        foreach (var probe in inspection.McpProbes)
        {
            switch (probe.Status)
            {
                case McpProbeStatus.Answered:
                    builder.AppendLine($"  {probe.ServerName} — answered server/discover");
                    builder.AppendLine($"      supports:     {Join(probe.SupportedVersions)}");
                    builder.AppendLine($"      capabilities: {Join(probe.Capabilities)}");
                    builder.AppendLine(
                        $"      identity:     {probe.SelfReportedName ?? "(none)"} "
                        + $"{probe.SelfReportedVersion ?? string.Empty}".TrimEnd()
                        + " (self-reported, not verified by the protocol)");
                    break;

                case McpProbeStatus.NoDiscovery:
                    builder.AppendLine(
                        $"  {probe.ServerName} — no server/discover, so a handshake-based revision "
                        + $"(2025-11-25 or earlier): {probe.Detail}");
                    break;

                case McpProbeStatus.NotProbed:
                    builder.AppendLine($"  {probe.ServerName} — not asked: {probe.Detail}");
                    break;

                default:
                    builder.AppendLine($"  {probe.ServerName} — could not be asked: {probe.Detail}");
                    break;
            }
        }
    }

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none reported)" : string.Join(", ", values);

    /// <summary>
    /// Writes the findings under two headings, because they answer different questions.
    /// </summary>
    /// <remarks>
    /// SF1015 means "SkillForge could not read this file, so the inventory above is incomplete". An SF8xxx finding means
    /// "here is something the inventory noticed". Printing both under "Could not read" said the second thing was the
    /// first, which was simply false — found by running the command against a fixture with both.
    /// </remarks>
    private static void AppendDiagnostics(StringBuilder builder, MigrationInspection inspection)
    {
        Append(builder, "Could not read:", inspection.Diagnostics
            .Where(diagnostic => !diagnostic.Code.StartsWith("SF8", StringComparison.Ordinal)));

        Append(builder, "MCP observations:", inspection.Diagnostics
            .Where(diagnostic => diagnostic.Code.StartsWith("SF8", StringComparison.Ordinal)));
    }

    private static void Append(StringBuilder builder, string heading, IEnumerable<Domain.Diagnostics.Diagnostic> findings)
    {
        var listed = findings.ToArray();

        if (listed.Length == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(heading);

        foreach (var diagnostic in listed)
        {
            builder.AppendLine($"  ! {diagnostic.Code} {diagnostic.Message}");
        }
    }

    private static string ToJson(MigrationInspection inspection)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["userDirectory"] = inspection.UserDirectory,
            ["projectDirectory"] = inspection.ProjectDirectory,
            ["providers"] = new JsonArray(
                [.. inspection.Providers.Select(provider => (JsonNode)new JsonObject
                {
                    ["id"] = provider.ProviderId,
                    ["displayName"] = provider.DisplayName,
                    ["present"] = provider.IsPresent,
                    ["configurationPaths"] = ToArray(provider.ConfigurationPaths),
                })]),
            ["skills"] = new JsonArray(
                [.. inspection.Skills.Select(skill => (JsonNode)new JsonObject
                {
                    ["provider"] = skill.ProviderId,
                    ["name"] = skill.Name,
                    ["directory"] = skill.Directory,
                    ["declaredCompatibility"] = ToArray(skill.DeclaredCompatibility),
                })]),
            ["mcpServers"] = new JsonArray(
                [.. inspection.McpServers.Select(server => (JsonNode)new JsonObject
                {
                    ["provider"] = server.ProviderId,
                    ["name"] = server.Name,
                    ["transport"] = server.Transport.ToString().ToLowerInvariant(),
                    ["command"] = server.Command,
                    ["arguments"] = ToArray(server.Arguments),

                    // Names, never values. See McpServerDeclaration.
                    ["environmentVariableNames"] = ToArray(server.EnvironmentVariableNames),
                    ["source"] = server.SourcePath,
                })]),
            ["instructionFiles"] = new JsonArray(
                [.. inspection.InstructionFiles.Select(file => (JsonNode)new JsonObject
                {
                    ["provider"] = file.ProviderId,
                    ["path"] = file.Path,
                    ["scope"] = file.Scope.ToString().ToLowerInvariant(),
                    ["sizeInBytes"] = file.SizeInBytes,
                })]),
            ["mcpProbes"] = new JsonArray(
                [.. inspection.McpProbes.Select(probe => (JsonNode)new JsonObject
                {
                    ["server"] = probe.ServerName,
                    ["status"] = probe.Status.ToString().ToLowerInvariant(),
                    ["supportedVersions"] = ToArray(probe.SupportedVersions),
                    ["capabilities"] = ToArray(probe.Capabilities),

                    // Named self-reported here too: a JSON consumer is the likeliest to treat it as verified.
                    ["selfReportedName"] = probe.SelfReportedName,
                    ["selfReportedVersion"] = probe.SelfReportedVersion,
                    ["detail"] = probe.Detail,
                })]),
            ["diagnostics"] = new JsonArray(
                [.. inspection.Diagnostics.Select(diagnostic => (JsonNode)new JsonObject
                {
                    ["code"] = diagnostic.Code,
                    ["severity"] = diagnostic.Severity.ToString().ToLowerInvariant(),
                    ["message"] = diagnostic.Message,
                    ["filePath"] = diagnostic.FilePath,
                })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonArray ToArray(IEnumerable<string> values) =>
        new([.. values.Select(value => (JsonNode)JsonValue.Create(value))]);
}
