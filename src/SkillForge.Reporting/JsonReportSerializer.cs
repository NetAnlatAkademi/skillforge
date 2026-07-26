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

        var document = new JsonObject
        {
            ["schemaVersion"] = SkillForgeTool.ReportSchemaVersion,
            ["tool"] = new JsonObject
            {
                ["name"] = SkillForgeTool.Name,
                ["version"] = SkillForgeTool.Version,
            },
            ["skill"] = new JsonObject
            {
                ["name"] = report.SkillName,
                ["path"] = report.SkillPath,
                ["version"] = report.SkillVersion,
            },
            ["summary"] = new JsonObject
            {
                ["errors"] = report.Summary.Errors,
                ["warnings"] = report.Summary.Warnings,
                ["info"] = report.Summary.Info,
                ["valid"] = report.IsValid,
            },
            ["diagnostics"] = diagnostics,
        };

        return document.ToJsonString(WriterOptions) + Environment.NewLine;
    }

    private static string ToText(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };
}
