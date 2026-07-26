using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge inspect</c> does.
/// </summary>
/// <remarks>
/// Inspect describes; it does not judge. The output says what the skill contains and what that implies, and
/// says so explicitly rather than implying a security verdict — so a clean inspection exits zero even when it
/// lists a script and three URLs.
/// </remarks>
internal sealed class InspectCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISkillLoader _loader;
    private readonly ISkillInspector _inspector;
    private readonly IFileSystem _fileSystem;
    private readonly IValidationReportRenderer _renderer;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads the skill.</param>
    /// <param name="inspector">Summarises it.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="renderer">Reports a skill that could not be loaded.</param>
    public InspectCommandRunner(
        ISkillLoader loader,
        ISkillInspector inspector,
        IFileSystem fileSystem,
        IValidationReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);

        _loader = loader;
        _inspector = inspector;
        _fileSystem = fileSystem;
        _renderer = renderer;
    }

    /// <summary>Inspects a skill and prints the summary.</summary>
    /// <param name="request">What to inspect and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(InspectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await _loader.LoadAsync(request.Path, cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess || load.Value is null)
        {
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(request.Path, load.Diagnostics),
                request.RenderOptions);

            return ExitCodes.ValidationFailed;
        }

        var inspection = await _inspector
            .InspectAsync(load.Value, cancellationToken)
            .ConfigureAwait(false);

        var text = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(inspection)
            : ToText(inspection, request);

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
            Console.Out.Write(text);
        }

        return ExitCodes.Success;
    }

    private static string ToText(SkillInspection inspection, InspectRequest request)
    {
        // Everything is shown by default; the --show-* flags narrow the output for scripts that want one
        // section, which is why "no flags" means "all sections".
        var showAll = !request.ShowFiles && !request.ShowLinks && !request.ShowPermissions;
        var builder = new System.Text.StringBuilder();

        builder.AppendLine($"Skill: {inspection.SkillName}");
        builder.AppendLine($"Path:  {inspection.SkillPath}");
        if (inspection.SkillVersion is { Length: > 0 } version)
        {
            builder.AppendLine($"Version: {version}");
        }

        if (showAll || request.ShowFiles)
        {
            builder.AppendLine();
            builder.AppendLine("Files:");
            foreach (var file in inspection.Files)
            {
                builder.AppendLine($"  {file.RelativePath} ({file.Kind}, {file.SizeInBytes} bytes)");
            }
        }

        if (showAll || request.ShowPermissions)
        {
            builder.AppendLine();
            builder.AppendLine("Detected capabilities:");
            foreach (var capability in inspection.Capabilities)
            {
                builder.AppendLine($"  {capability}");
            }

            builder.AppendLine();
            builder.AppendLine("Declared tools:");
            if (inspection.DeclaredTools.Count == 0)
            {
                builder.AppendLine("  (none declared)");
            }
            else
            {
                foreach (var tool in inspection.DeclaredTools)
                {
                    builder.AppendLine($"  {tool}");
                }
            }
        }

        if (showAll || request.ShowLinks)
        {
            builder.AppendLine();
            builder.AppendLine("External URLs:");
            if (inspection.ExternalUrls.Count == 0)
            {
                builder.AppendLine("  (none found in SKILL.md)");
            }
            else
            {
                foreach (var url in inspection.ExternalUrls)
                {
                    builder.AppendLine($"  {url}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("Observations:");
        builder.AppendLine($"  {inspection.Diagnostics.Count} noted, "
            + $"{inspection.Warnings} warnings, {inspection.Errors} errors");
        builder.AppendLine();
        builder.AppendLine("This is a description of what the skill contains, not a security verdict.");

        return builder.ToString();
    }

    private static string ToJson(SkillInspection inspection)
    {
        var files = new JsonArray();
        foreach (var file in inspection.Files)
        {
            files.Add(new JsonObject
            {
                ["path"] = file.RelativePath,
                ["kind"] = file.Kind.ToString(),
                ["sizeInBytes"] = file.SizeInBytes,
            });
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["skill"] = new JsonObject
            {
                ["name"] = inspection.SkillName,
                ["path"] = inspection.SkillPath,
                ["version"] = inspection.SkillVersion,
            },
            ["files"] = files,
            ["capabilities"] = ToArray(inspection.Capabilities),
            ["declaredTools"] = ToArray(inspection.DeclaredTools),
            ["externalUrls"] = ToArray(inspection.ExternalUrls),
            ["observations"] = new JsonArray(
                [.. inspection.Diagnostics.Select(diagnostic => (JsonNode)new JsonObject
                {
                    ["code"] = diagnostic.Code,
                    ["message"] = diagnostic.Message,
                    ["filePath"] = diagnostic.FilePath,
                })]),
            ["disclaimer"] = "A description of the skill's contents, not a security verdict.",
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonArray ToArray(IReadOnlyList<string> values) =>
        new([.. values.Select(value => (JsonNode)JsonValue.Create(value))]);
}

/// <summary>
/// Everything <c>inspect</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory or <c>SKILL.md</c> path.</param>
/// <param name="Format">Console or JSON.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="ShowFiles">Show the file inventory.</param>
/// <param name="ShowLinks">Show external URLs.</param>
/// <param name="ShowPermissions">Show inferred capabilities and declared tools.</param>
/// <param name="RenderOptions">How to present a load failure.</param>
internal sealed record InspectRequest(
    string Path,
    string Format,
    string? OutputPath,
    bool ShowFiles,
    bool ShowLinks,
    bool ShowPermissions,
    ReportRenderOptions RenderOptions);
