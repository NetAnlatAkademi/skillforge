using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Mcp;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge mcp inspect</c>, <c>mcp validate</c> and <c>mcp diff</c> do.
/// </summary>
/// <remarks>
/// These take a **file**, where <c>migrate inspect</c> finds the files a provider owns. That is the difference
/// between "what is installed on this machine" and "what does this configuration in this pull request declare",
/// and the second question is the one a CI step asks.
///
/// <c>inspect</c> and <c>validate</c> run the same checks and differ only in what they do with the result: inspect
/// reports and exits zero, validate treats a finding as a gate. The severity of an <c>SF8xxx</c> finding does not
/// change between them — what a command does with a finding is the command's decision, and a published code's
/// meaning is not.
///
/// A stdio server is never launched, with or without <c>--probe-mcp</c>.
/// </remarks>
internal sealed class McpCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly McpFileInspector _inspector;
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the runner.</summary>
    /// <param name="inspector">Reads and inspects a configuration file.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    public McpCommandRunner(McpFileInspector inspector, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _inspector = inspector;
        _fileSystem = fileSystem;
    }

    /// <summary>Inspects one configuration file.</summary>
    /// <param name="request">What to inspect and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/>, or <see cref="ExitCodes.ValidationFailed"/> when the file could not be read
    /// — or, under <c>validate</c>, when there is anything to report.
    /// </returns>
    internal async Task<int> InspectAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inspection = await _inspector
            .InspectAsync(request.Path, request.Probe, cancellationToken)
            .ConfigureAwait(false);

        await WriteAsync(
            request.Format,
            request.OutputPath,
            () => ToJson(inspection),
            () => ToText(inspection, request.Gate),
            cancellationToken).ConfigureAwait(false);

        // A file that could not be read fails either command: "no servers" and "no answer" are different facts and
        // only one of them is reassuring.
        if (inspection.Diagnostics.Any(finding =>
            finding.Code == DiagnosticCodes.ProviderConfigurationNotParsable))
        {
            return ExitCodes.ValidationFailed;
        }

        return request.Gate && inspection.HasFindings ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    /// <summary>Compares two configuration files.</summary>
    /// <param name="request">What to compare and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> DiffAsync(McpDiffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var before = await _inspector
            .InspectAsync(request.BeforePath, probe: false, cancellationToken)
            .ConfigureAwait(false);

        var after = await _inspector
            .InspectAsync(request.AfterPath, probe: false, cancellationToken)
            .ConfigureAwait(false);

        var unreadable = before.Diagnostics
            .Concat(after.Diagnostics)
            .Where(finding => finding.Code == DiagnosticCodes.ProviderConfigurationNotParsable)
            .ToArray();

        var diff = McpConfigurationDiffer.Compare(before, after);

        await WriteAsync(
            request.Format,
            request.OutputPath,
            () => ToJson(diff, unreadable),
            () => ToText(diff, unreadable),
            cancellationToken).ConfigureAwait(false);

        if (unreadable.Length > 0)
        {
            return ExitCodes.ValidationFailed;
        }

        return request.FailOnChange && diff.HasChanges ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    private async Task WriteAsync(
        string format,
        string? outputPath,
        Func<string> json,
        Func<string> text,
        CancellationToken cancellationToken)
    {
        var content = string.Equals(format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? json()
            : text();

        if (outputPath is not { Length: > 0 })
        {
            await Console.Out.WriteAsync(content).ConfigureAwait(false);
            return;
        }

        var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
        if (directory is { Length: > 0 })
        {
            _fileSystem.CreateDirectory(directory);
        }

        await _fileSystem.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
    }

    private static string ToText(McpConfigurationInspection inspection, bool gate)
    {
        var builder = new StringBuilder();

        builder.AppendLine(gate ? "SkillForge MCP Validate" : "SkillForge MCP Inspect");
        builder.AppendLine();
        builder.AppendLine($"File:    {inspection.Path}");
        builder.AppendLine($"Servers: {inspection.Servers.Count}");
        builder.AppendLine();

        foreach (var server in inspection.Servers)
        {
            builder.AppendLine($"  {server.Name} — {server.Transport}");
            builder.AppendLine($"      {(server.Transport == McpTransport.Http ? "url" : "command")}: "
                + $"{server.Command ?? "(none declared)"}");

            if (server.Arguments.Count > 0)
            {
                builder.AppendLine($"      arguments: {string.Join(' ', server.Arguments)}");
            }

            if (server.EnvironmentVariableNames.Count > 0)
            {
                builder.AppendLine(
                    $"      environment: {string.Join(", ", server.EnvironmentVariableNames)} (names only)");
            }
        }

        if (inspection.Servers.Count == 0)
        {
            builder.AppendLine("  (none declared)");
        }

        AppendProbes(builder, inspection.Probes);
        AppendFindings(builder, inspection.Diagnostics);

        builder.AppendLine();
        builder.AppendLine(
            "Environment variable values are never read or printed — only the names a declaration sets.");

        if (inspection.Probes.Count == 0)
        {
            builder.AppendLine(
                "No server was asked about itself. Add --probe-mcp for that; a local stdio server is never "
                    + "launched either way.");
        }

        return builder.ToString();
    }

    private static void AppendProbes(StringBuilder builder, IReadOnlyList<McpServerProbe> probes)
    {
        if (probes.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Protocol probe:");

        foreach (var probe in probes)
        {
            builder.AppendLine($"  {probe.ServerName} — {Describe(probe)}");
        }
    }

    private static string Describe(McpServerProbe probe) => probe.Status switch
    {
        McpProbeStatus.Answered =>
            $"answered, speaking {probe.AnsweredRevision ?? "an unnamed revision"}; supports "
                + $"{Join(probe.SupportedVersions)}; capabilities {Join(probe.Capabilities)}",

        McpProbeStatus.NoDiscovery =>
            $"no server/discover, so a handshake-based revision (2025-11-25 or earlier): {probe.Detail}",

        McpProbeStatus.RequiresAuthorization =>
            $"requires {probe.Authorization?.Scheme ?? "unknown"} authorization",

        McpProbeStatus.NotProbed => $"not asked — {probe.Detail}",

        _ => probe.Detail ?? "did not answer",
    };

    private static void AppendFindings(StringBuilder builder, IReadOnlyList<Diagnostic> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Findings:");

        foreach (var finding in findings)
        {
            builder.AppendLine($"  {finding.Code} {finding.Message}");
        }
    }

    private static string ToText(McpConfigurationDiff diff, Diagnostic[] unreadable)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge MCP Diff");
        builder.AppendLine();
        builder.AppendLine($"Before: {diff.BeforePath}");
        builder.AppendLine($"After:  {diff.AfterPath}");
        builder.AppendLine();

        if (unreadable.Length > 0)
        {
            builder.AppendLine("Could not read:");
            foreach (var finding in unreadable)
            {
                builder.AppendLine($"  {finding.Code} {finding.Message}");
            }

            return builder.ToString();
        }

        if (!diff.HasChanges)
        {
            builder.AppendLine("The two configurations would connect to the same servers, the same way.");
            return builder.ToString();
        }

        if (diff.ReachGrew)
        {
            builder.AppendLine("An agent would now reach something it did not reach before.");
            builder.AppendLine();
        }

        AppendList(builder, "Servers added", diff.ServersAdded);
        AppendList(builder, "Servers removed", diff.ServersRemoved);

        foreach (var change in diff.Changed)
        {
            builder.AppendLine($"{change.Name} changed:");
            AppendValue(builder, "transport", change.Transport);
            AppendValue(builder, "command/url", change.Command);
            AppendList(builder, "  arguments added", change.Arguments.Added, indent: 4);
            AppendList(builder, "  arguments removed", change.Arguments.Removed, indent: 4);
            AppendList(builder, "  environment added", change.EnvironmentVariableNames.Added, indent: 4);
            AppendList(builder, "  environment removed", change.EnvironmentVariableNames.Removed, indent: 4);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendValue(StringBuilder builder, string label, SurfaceValueChange? change)
    {
        if (change is null)
        {
            return;
        }

        builder.AppendLine($"  {label}: {change.Before ?? "(none)"} -> {change.After ?? "(none)"}");
    }

    private static void AppendList(
        StringBuilder builder,
        string label,
        IReadOnlyList<string> entries,
        int indent = 2)
    {
        if (entries.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{label}:");
        foreach (var entry in entries)
        {
            builder.AppendLine($"{new string(' ', indent)}{entry}");
        }

        builder.AppendLine();
    }

    private static string ToJson(McpConfigurationInspection inspection)
    {
        var servers = new JsonArray();
        foreach (var server in inspection.Servers)
        {
            servers.Add(new JsonObject
            {
                ["name"] = server.Name,
                ["transport"] = server.Transport.ToString().ToLowerInvariant(),
                ["command"] = server.Command,
                ["arguments"] = Array(server.Arguments),

                // Names only, and the key says so: see McpServerDeclaration for why no value is ever read.
                ["environmentVariableNames"] = Array(server.EnvironmentVariableNames),
            });
        }

        var probes = new JsonArray();
        foreach (var probe in inspection.Probes)
        {
            probes.Add(new JsonObject
            {
                ["server"] = probe.ServerName,
                ["status"] = probe.Status.ToString(),
                ["answeredRevision"] = probe.AnsweredRevision,
                ["supportedVersions"] = Array(probe.SupportedVersions),
                ["capabilities"] = Array(probe.Capabilities),
                ["selfReportedName"] = probe.SelfReportedName,
                ["selfReportedVersion"] = probe.SelfReportedVersion,
                ["detail"] = probe.Detail,
            });
        }

        return Document(new JsonObject
        {
            ["file"] = inspection.Path,
            ["servers"] = servers,
            ["probes"] = probes,
            ["findings"] = Findings(inspection.Diagnostics),
        });
    }

    private static string ToJson(McpConfigurationDiff diff, Diagnostic[] unreadable)
    {
        var changed = new JsonArray();
        foreach (var change in diff.Changed)
        {
            changed.Add(new JsonObject
            {
                ["name"] = change.Name,
                ["transport"] = Value(change.Transport),
                ["command"] = Value(change.Command),
                ["arguments"] = Set(change.Arguments),
                ["environmentVariableNames"] = Set(change.EnvironmentVariableNames),
            });
        }

        return Document(new JsonObject
        {
            ["before"] = diff.BeforePath,
            ["after"] = diff.AfterPath,
            ["hasChanges"] = diff.HasChanges,
            ["reachGrew"] = diff.ReachGrew,
            ["serversAdded"] = Array(diff.ServersAdded),
            ["serversRemoved"] = Array(diff.ServersRemoved),
            ["changed"] = changed,
            ["findings"] = Findings(unreadable),
        });
    }

    private static string Document(JsonObject body)
    {
        body["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion;
        body["tool"] = new JsonObject
        {
            ["name"] = Reporting.SkillForgeTool.Name,
            ["version"] = Reporting.SkillForgeTool.Version,
        };

        return body.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject? Value(SurfaceValueChange? change) =>
        change is null ? null : new JsonObject { ["before"] = change.Before, ["after"] = change.After };

    private static JsonObject Set(SurfaceSetDiff diff) => new()
    {
        ["added"] = Array(diff.Added),
        ["removed"] = Array(diff.Removed),
    };

    private static JsonArray Array(IReadOnlyList<string> values) =>
        new([.. values.Select(value => (JsonNode)JsonValue.Create(value))]);

    private static JsonArray Findings(IEnumerable<Diagnostic> findings) =>
        new(
        [
            .. findings.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            }),
        ]);

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}
