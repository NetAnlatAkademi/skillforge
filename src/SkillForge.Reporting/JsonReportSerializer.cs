using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;

namespace SkillForge.Reporting;

/// <summary>
/// Serialises a validation report as SkillForge's own JSON format.
/// </summary>
/// <remarks>
/// The shape is a published contract, so it is written by hand with <see cref="JsonNode"/> rather than by
/// serialising the domain records. That way a rename inside the domain cannot silently change the output
/// somebody's CI script parses.
/// </remarks>
public sealed class JsonReportSerializer : IValidationReportSerializer
{
    private static readonly JsonSerializerOptions WriterOptions = new() { WriteIndented = true };

    /// <inheritdoc />
    public string Format => "json";

    /// <inheritdoc />
    public string Serialize(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var document = new JsonObject
        {
            ["schemaVersion"] = SkillForgeTool.ReportSchemaVersion,
            ["tool"] = ToolObject(),
            ["skill"] = SkillObject(report),
            ["summary"] = SummaryObject(report),
            ["diagnostics"] = DiagnosticsArray(report),
        };

        return document.ToJsonString(WriterOptions) + Environment.NewLine;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A batch nests each skill under <c>skills</c>, with its own summary and diagnostics, plus a run-level
    /// summary totalled across all of them. The single-skill document is untouched, so a consumer written
    /// against it keeps working; a consumer that wants batches looks for <c>skills</c>.
    /// </remarks>
    public string SerializeRun(ValidationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var skills = new JsonArray();
        foreach (var report in run.Skills)
        {
            skills.Add(new JsonObject
            {
                ["skill"] = SkillObject(report),
                ["summary"] = SummaryObject(report),
                ["diagnostics"] = DiagnosticsArray(report),
            });
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = SkillForgeTool.ReportSchemaVersion,
            ["tool"] = ToolObject(),
            ["root"] = run.RootPath,
            ["summary"] = new JsonObject
            {
                ["skills"] = run.SkillCount,
                ["invalidSkills"] = run.InvalidSkillCount,
                ["errors"] = run.Summary.Errors,
                ["warnings"] = run.Summary.Warnings,
                ["info"] = run.Summary.Info,
                ["valid"] = run.IsValid,
                ["suppressed"] = run.SuppressedCount,
            },
            ["skills"] = skills,
        };

        return document.ToJsonString(WriterOptions) + Environment.NewLine;
    }

    private static JsonObject ToolObject() => new()
    {
        ["name"] = SkillForgeTool.Name,
        ["version"] = SkillForgeTool.Version,
    };

    private static JsonObject SkillObject(ValidationReport report) => new()
    {
        ["name"] = report.SkillName,
        ["path"] = report.SkillPath,
        ["version"] = report.SkillVersion,
    };

    private static JsonObject SummaryObject(ValidationReport report) => new()
    {
        ["errors"] = report.Summary.Errors,
        ["warnings"] = report.Summary.Warnings,
        ["info"] = report.Summary.Info,
        ["valid"] = report.IsValid,
        ["suppressed"] = report.SuppressedCount,
    };

    private static JsonArray DiagnosticsArray(ValidationReport report)
    {
        var diagnostics = new JsonArray();
        foreach (var diagnostic in report.Diagnostics)
        {
            diagnostics.Add(new JsonObject
            {
                ["code"] = diagnostic.Code,
                ["severity"] = ToText(diagnostic.Severity),
                ["message"] = diagnostic.Message,
                ["filePath"] = diagnostic.FilePath,
                ["line"] = diagnostic.Line,
                ["suggestion"] = diagnostic.Suggestion,
            });
        }

        return diagnostics;
    }

    private static string ToText(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };
}
