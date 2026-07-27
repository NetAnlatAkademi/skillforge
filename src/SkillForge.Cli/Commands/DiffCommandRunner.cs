using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Diffing;
using SkillForge.Application.Inspection;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge diff</c> does.
/// </summary>
/// <remarks>
/// Git already shows which bytes changed. What a reviewer cannot see from a patch is that a skill quietly gained a
/// permission, a script, or a new host to talk to — so this reports the behaviour surface, and leads with the three
/// changes that widen a skill's reach.
///
/// New errors make it exit 1: a change that introduces a validation error is a regression by any definition. A
/// surface change on its own is information, not a failure, unless the caller asks for that with
/// <c>--fail-on-change</c> — whether a new permission is acceptable is a policy SkillForge does not own.
/// </remarks>
internal sealed class DiffCommandRunner
{
    private const int IndentWidth = 2;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISkillLoader _loader;
    private readonly ISkillInspector _inspector;
    private readonly ISkillValidator _validator;
    private readonly IFileSystem _fileSystem;
    private readonly IValidationReportRenderer _renderer;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads each side.</param>
    /// <param name="inspector">Computes each side's surface.</param>
    /// <param name="validator">Validates each side, so findings can be compared.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="renderer">Reports a side that could not be loaded.</param>
    public DiffCommandRunner(
        ISkillLoader loader,
        ISkillInspector inspector,
        ISkillValidator validator,
        IFileSystem fileSystem,
        IValidationReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);

        _loader = loader;
        _inspector = inspector;
        _validator = validator;
        _fileSystem = fileSystem;
        _renderer = renderer;
    }

    /// <summary>Compares two versions of a skill.</summary>
    /// <param name="request">What to compare and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(DiffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var before = await SnapshotAsync(request.BeforePath, request, cancellationToken).ConfigureAwait(false);
        if (before is null)
        {
            return ExitCodes.ValidationFailed;
        }

        var after = await SnapshotAsync(request.AfterPath, request, cancellationToken).ConfigureAwait(false);
        if (after is null)
        {
            return ExitCodes.ValidationFailed;
        }

        var diff = SkillSurfaceDiffer.Compare(before, after);

        var text = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(diff)
            : ToText(diff);

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

        if (diff.NewErrors.Count > 0)
        {
            return ExitCodes.ValidationFailed;
        }

        return request.FailOnChange && diff.HasChanges ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    /// <summary>
    /// Loads, inspects and validates one side. A side that cannot be loaded is reported the same way any other
    /// unloadable skill is, because "the before version is broken" is a real answer the user needs.
    /// </summary>
    private async Task<SkillSnapshot?> SnapshotAsync(
        string path,
        DiffRequest request,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess || load.Value is null)
        {
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(path, load.Diagnostics),
                request.RenderOptions);

            return null;
        }

        var inspection = await _inspector.InspectAsync(load.Value, cancellationToken).ConfigureAwait(false);
        var report = await _validator.ValidateAsync(load.Value, cancellationToken).ConfigureAwait(false);

        return new SkillSnapshot(path, load.Value, inspection, report);
    }

    private static string ToText(SkillSurfaceDiff diff)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Diff");
        builder.AppendLine();
        builder.AppendLine($"Before: {diff.BeforePath}");
        builder.AppendLine($"After:  {diff.AfterPath}");
        builder.AppendLine();

        if (!diff.HasChanges)
        {
            builder.AppendLine("The behaviour surface is unchanged.");
            return builder.ToString();
        }

        // Reach first: a reviewer with time for one line should read the one that matters.
        if (diff.ReachGrew)
        {
            builder.AppendLine("The skill can now do more than before:");
            AppendSet(builder, "Permissions added", diff.DeclaredTools.Added, depth: 1);
            AppendSet(builder, "Scripts added", diff.Scripts.Added, depth: 1);
            AppendSet(builder, "Domains added", diff.ExternalDomains.Added, depth: 1);
        }

        AppendValue(builder, "Name", diff.Name);
        AppendValue(builder, "Version", diff.Version);

        if (diff.Description is not null)
        {
            // No claim about whether this broadened the activation scope — see SkillSurfaceDiff's remarks.
            builder.AppendLine("Description changed:");
            builder.AppendLine($"  before: {diff.Description.Before ?? "(none)"}");
            builder.AppendLine($"  after:  {diff.Description.After ?? "(none)"}");
            builder.AppendLine();
        }

        AppendSet(builder, "Permissions removed", diff.DeclaredTools.Removed);
        AppendSet(builder, "Scripts removed", diff.Scripts.Removed);
        AppendSet(builder, "Domains removed", diff.ExternalDomains.Removed);
        AppendSet(builder, "Compatibility added", diff.Compatibility.Added);
        AppendSet(builder, "Compatibility removed", diff.Compatibility.Removed);
        AppendSet(builder, "Files added", diff.Files.Added);
        AppendSet(builder, "Files removed", diff.Files.Removed);

        AppendFindings(builder, "New findings", diff.NewFindings);
        AppendFindings(builder, "Resolved findings", diff.ResolvedFindings);

        return builder.ToString();
    }

    private static void AppendValue(StringBuilder builder, string label, SurfaceValueChange? change)
    {
        if (change is null)
        {
            return;
        }

        builder.AppendLine($"{label}: {change.Before ?? "(none)"} -> {change.After ?? "(none)"}");
        builder.AppendLine();
    }

    /// <summary>Writes a labelled list, or nothing when the list is empty.</summary>
    /// <param name="builder">Buffer being written to.</param>
    /// <param name="label">Label for the group.</param>
    /// <param name="entries">Entries to list.</param>
    /// <param name="depth">
    /// How far to indent the label. Entries always sit one level deeper, so a nested group reads as nested rather
    /// than as a label with a list beside it.
    /// </param>
    private static void AppendSet(
        StringBuilder builder,
        string label,
        IReadOnlyList<string> entries,
        int depth = 0)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var labelIndent = new string(' ', depth * IndentWidth);
        var entryIndent = new string(' ', (depth + 1) * IndentWidth);

        builder.AppendLine($"{labelIndent}{label}:");
        foreach (var entry in entries)
        {
            builder.AppendLine($"{entryIndent}{entry}");
        }

        builder.AppendLine();
    }

    private static void AppendFindings(
        StringBuilder builder,
        string label,
        IReadOnlyList<Diagnostic> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{label}:");
        foreach (var finding in findings)
        {
            var location = finding.FilePath is { Length: > 0 } file
                ? finding.Line is { } line ? $" ({file}:{line})" : $" ({file})"
                : string.Empty;

            builder.AppendLine($"  {finding.Code} {finding.Message}{location}");
        }

        builder.AppendLine();
    }

    private static string ToJson(SkillSurfaceDiff diff)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = Reporting.SkillForgeTool.Name,
                ["version"] = Reporting.SkillForgeTool.Version,
            },
            ["before"] = diff.BeforePath,
            ["after"] = diff.AfterPath,
            ["hasChanges"] = diff.HasChanges,
            ["reachGrew"] = diff.ReachGrew,
            ["name"] = ToJson(diff.Name),
            ["version"] = ToJson(diff.Version),
            ["description"] = ToJson(diff.Description),
            ["declaredTools"] = ToJson(diff.DeclaredTools),
            ["compatibility"] = ToJson(diff.Compatibility),
            ["externalDomains"] = ToJson(diff.ExternalDomains),
            ["scripts"] = ToJson(diff.Scripts),
            ["files"] = ToJson(diff.Files),
            ["newFindings"] = ToJson(diff.NewFindings),
            ["resolvedFindings"] = ToJson(diff.ResolvedFindings),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject? ToJson(SurfaceValueChange? change) =>
        change is null
            ? null
            : new JsonObject { ["before"] = change.Before, ["after"] = change.After };

    private static JsonObject ToJson(SurfaceSetDiff diff) => new()
    {
        ["added"] = new JsonArray([.. diff.Added.Select(value => (JsonNode)JsonValue.Create(value))]),
        ["removed"] = new JsonArray([.. diff.Removed.Select(value => (JsonNode)JsonValue.Create(value))]),
    };

    private static JsonArray ToJson(IReadOnlyList<Diagnostic> findings) =>
        new(
        [
            .. findings.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
                ["line"] = finding.Line,
            }),
        ]);
}
