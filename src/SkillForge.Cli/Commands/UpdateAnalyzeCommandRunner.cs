using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Provenance;
using SkillForge.Application.Updates;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Updates;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge update analyze</c> does.
/// </summary>
/// <remarks>
/// The command the auto-updating plugin made necessary. Comparing versions was already possible; what was not was
/// asking whether the version that will arrive on its own can do more than the one somebody reviewed.
///
/// It reuses the capability scan and the provenance reader rather than parsing anything a second time, and it
/// takes two directories rather than a version range: SkillForge does not download anything, so both sides have
/// to be on disk. <c>docs/ci.md</c> carries the <c>git worktree</c> recipe that materialises the other side.
/// </remarks>
internal sealed class UpdateAnalyzeCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ProvenanceInspector _provenance;
    private readonly CapabilitySurfaceScanner _surfaces;
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    /// <summary>Initialises the runner.</summary>
    /// <param name="provenance">Reads where each side came from.</param>
    /// <param name="surfaces">Reads what each side can reach.</param>
    /// <param name="fileSystem">Resolves paths and writes machine-readable output when asked.</param>
    /// <param name="serializers">Report serialisers, used for <c>--format sarif</c>.</param>
    public UpdateAnalyzeCommandRunner(
        ProvenanceInspector provenance,
        CapabilitySurfaceScanner surfaces,
        IFileSystem fileSystem,
        IEnumerable<IValidationReportSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(serializers);

        _provenance = provenance;
        _surfaces = surfaces;
        _fileSystem = fileSystem;
        _serializers = [.. serializers];
    }

    /// <summary>Analyses what one update does.</summary>
    /// <param name="request">What to compare and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/> unless <c>--fail-on-expansion</c> was given and the update expands what the
    /// asset can reach.
    /// </returns>
    internal async Task<int> RunAsync(UpdateAnalyzeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var path in new[] { request.BasePath, request.TargetPath })
        {
            if (!_fileSystem.DirectoryExists(path))
            {
                await Console.Error
                    .WriteLineAsync($"'{path}' is not a directory, so there is no update to analyse.")
                    .ConfigureAwait(false);

                return ExitCodes.InvalidUsage;
            }
        }

        var beforeProvenance = await _provenance
            .InspectAsync(request.BasePath, includeLocalModifications: true, cancellationToken)
            .ConfigureAwait(false);

        var afterProvenance = await _provenance
            .InspectAsync(request.TargetPath, includeLocalModifications: true, cancellationToken)
            .ConfigureAwait(false);

        var beforeSurface = await _surfaces
            .ScanAsync(request.BasePath, cancellationToken).ConfigureAwait(false);
        var afterSurface = await _surfaces
            .ScanAsync(request.TargetPath, cancellationToken).ConfigureAwait(false);

        var analysis = UpdateAnalyzer.Analyze(
            request.BasePath,
            request.TargetPath,
            UpdateAnalyzer.ModeOf(afterProvenance),
            ProvenanceDiffer.Compare(beforeProvenance, afterProvenance),
            CapabilitySurfaceDiff.Between(beforeSurface, afterSurface));

        var content = request.Format switch
        {
            _ when string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase) =>
                ToJson(analysis),
            _ when string.Equals(request.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase) =>
                ToSarif(analysis),
            _ => ToText(analysis),
        };

        if (request.OutputPath is { Length: > 0 } outputPath)
        {
            var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
            if (directory is { Length: > 0 })
            {
                _fileSystem.CreateDirectory(directory);
            }

            await _fileSystem.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Console.Out.WriteAsync(content).ConfigureAwait(false);
        }

        return request.FailOnExpansion && analysis.Capabilities.Expands
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    private static string ToText(UpdateAnalysis analysis)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Update Analysis");
        builder.AppendLine();
        builder.AppendLine($"Base:        {analysis.BasePath}");
        builder.AppendLine($"Target:      {analysis.TargetPath}");
        builder.AppendLine($"Update mode: {analysis.UpdateMode}");
        builder.AppendLine();

        AppendVersions(builder, analysis);
        AppendSurface(builder, analysis.Capabilities);
        AppendProvenance(builder, analysis);

        builder.AppendLine($"Risk:   {analysis.Risk.ToString().ToUpperInvariant()}");
        builder.AppendLine($"Reason: {analysis.Reason}");

        if (analysis.Findings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Findings:");

            foreach (var finding in analysis.Findings)
            {
                builder.AppendLine($"  {Mark(finding.Severity)} {finding.Code} {finding.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            "A credential source is an environment variable whose name looks like one. SkillForge never reads "
                + "the value.");

        return builder.ToString();
    }

    private static void AppendVersions(StringBuilder builder, UpdateAnalysis analysis)
    {
        var versions = analysis.Provenance.Changed
            .Where(change => change.Version is not null)
            .ToArray();

        if (versions.Length == 0)
        {
            return;
        }

        builder.AppendLine("Versions:");

        foreach (var change in versions)
        {
            builder.AppendLine(
                $"  {change.Name}: {change.Version!.Before ?? "unknown"} -> {change.Version.After ?? "unknown"}");
        }

        builder.AppendLine();
    }

    private static void AppendSurface(StringBuilder builder, CapabilitySurfaceDiff capabilities)
    {
        if (!capabilities.HasChanges)
        {
            builder.AppendLine("Changes: none. The update reaches exactly as far as what it replaces.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("Changes:");

        AppendSet(builder, "skill", capabilities.Skills);
        AppendSet(builder, "MCP server", capabilities.McpServers);
        AppendSet(builder, "hook", capabilities.Hooks);
        AppendSet(builder, "script", capabilities.Scripts);
        AppendSet(builder, "host", capabilities.Domains);
        AppendSet(builder, "credential source", capabilities.CredentialSources);
        AppendSet(builder, "capability", capabilities.Capabilities);

        builder.AppendLine();
    }

    private static void AppendSet(StringBuilder builder, string noun, SurfaceSetDiff diff)
    {
        foreach (var added in diff.Added)
        {
            builder.AppendLine($"  + {noun}: {added}");
        }

        foreach (var removed in diff.Removed)
        {
            builder.AppendLine($"  - {noun}: {removed}");
        }
    }

    private static void AppendProvenance(StringBuilder builder, UpdateAnalysis analysis)
    {
        var drifted = analysis.Provenance.Changed
            .Where(change => change.Publisher is not null
                || change.Marketplace is not null
                || change.UpdateMode is not null)
            .ToArray();

        if (drifted.Length == 0)
        {
            return;
        }

        builder.AppendLine("Distribution:");

        foreach (var change in drifted)
        {
            AppendChange(builder, change.Name, "publisher", change.Publisher);
            AppendChange(builder, change.Name, "marketplace", change.Marketplace);
            AppendChange(builder, change.Name, "update mode", change.UpdateMode);
        }

        builder.AppendLine();
    }

    private static void AppendChange(StringBuilder builder, string name, string label, SurfaceValueChange? change)
    {
        if (change is null)
        {
            return;
        }

        builder.AppendLine(
            $"  {name}: {label} {change.Before ?? "unknown"} -> {change.After ?? "unknown"}");
    }

    private static string Mark(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "x",
        DiagnosticSeverity.Warning => "!",
        _ => "i",
    };

    private string ToSarif(UpdateAnalysis analysis)
    {
        var report = new ValidationReport(
            string.Empty,
            analysis.TargetPath,
            analysis.Findings,
            ValidationSummary.FromDiagnostics(analysis.Findings));

        return _serializers
            .Single(candidate =>
                string.Equals(candidate.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase))
            .Serialize(report);
    }

    private static string ToJson(UpdateAnalysis analysis)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = Reporting.SkillForgeTool.Name,
                ["version"] = Reporting.SkillForgeTool.Version,
            },
            ["base"] = analysis.BasePath,
            ["target"] = analysis.TargetPath,
            ["updateMode"] = analysis.UpdateMode.ToString().ToLowerInvariant(),
            ["risk"] = analysis.Risk.ToString().ToLowerInvariant(),
            ["reason"] = analysis.Reason,
            ["expands"] = analysis.Capabilities.Expands,
            ["capabilities"] = new JsonObject
            {
                ["skills"] = ToJson(analysis.Capabilities.Skills),
                ["mcpServers"] = ToJson(analysis.Capabilities.McpServers),
                ["hooks"] = ToJson(analysis.Capabilities.Hooks),
                ["scripts"] = ToJson(analysis.Capabilities.Scripts),
                ["domains"] = ToJson(analysis.Capabilities.Domains),

                // Names of environment variables, never values. See CapabilitySurface.
                ["credentialSources"] = ToJson(analysis.Capabilities.CredentialSources),
                ["implied"] = ToJson(analysis.Capabilities.Capabilities),
            },
            ["distribution"] = new JsonArray([.. analysis.Provenance.Changed.Select(change => (JsonNode)new JsonObject
            {
                ["name"] = change.Name,
                ["path"] = change.Path,
                ["publisher"] = ToJson(change.Publisher),
                ["marketplace"] = ToJson(change.Marketplace),
                ["version"] = ToJson(change.Version),
                ["updateMode"] = ToJson(change.UpdateMode),
                ["sha256"] = ToJson(change.Fingerprint),
            })]),
            ["findings"] = new JsonArray([.. analysis.Findings.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject ToJson(SurfaceSetDiff diff) => new()
    {
        ["added"] = new JsonArray([.. diff.Added.Select(value => (JsonNode)JsonValue.Create(value))]),
        ["removed"] = new JsonArray([.. diff.Removed.Select(value => (JsonNode)JsonValue.Create(value))]),
    };

    private static JsonObject? ToJson(SurfaceValueChange? change) =>
        change is null ? null : new JsonObject { ["before"] = change.Before, ["after"] = change.After };
}
