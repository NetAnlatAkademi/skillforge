using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;

namespace SkillForge.Reporting;

/// <summary>
/// Serialises a validation report as SARIF 2.1.0, the format GitHub code scanning reads.
/// </summary>
/// <remarks>
/// Two details matter for the annotations to appear on a pull request. Rules are declared once under
/// <c>tool.driver.rules</c> and referenced by index, and every result carries a location with a URI
/// relative to the repository — an absolute path on the build agent would not match any file GitHub knows
/// about. Diagnostics with no file are still emitted, without a location, rather than dropped.
/// </remarks>
public sealed class SarifReportSerializer : IValidationReportSerializer
{
    private const string SarifVersion = "2.1.0";
    private const string SarifSchema =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";

    private static readonly JsonSerializerOptions WriterOptions = new() { WriteIndented = true };

    /// <inheritdoc />
    public string Format => "sarif";

    /// <inheritdoc />
    public string Serialize(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return Build([.. report.Diagnostics.Select(diagnostic => new Finding(report.SkillPath, diagnostic))]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A batch becomes one SARIF run, not one per skill. That is what a code-scanning upload wants: a single
    /// file covers every skill in the repository, and each result carries its own skill-relative path, so the
    /// annotations still land on the right files.
    /// </remarks>
    public string SerializeRun(ValidationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return Build(
        [
            .. run.Skills.SelectMany(report =>
                report.Diagnostics.Select(diagnostic => new Finding(report.SkillPath, diagnostic))),
        ]);
    }

    /// <summary>A diagnostic together with the skill it came from, which is what the location needs.</summary>
    private readonly record struct Finding(string SkillPath, Diagnostic Diagnostic);

    private static string Build(IReadOnlyList<Finding> findings)
    {
        // Each distinct code becomes one rule declaration; results point at it by index.
        var codes = findings
            .Select(finding => finding.Diagnostic.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var ruleIndex = codes
            .Select((code, index) => (code, index))
            .ToDictionary(pair => pair.code, pair => pair.index, StringComparer.Ordinal);

        // One lookup built up front so each code resolves its example diagnostic in constant time,
        // rather than rescanning the full diagnostics list once per code.
        var firstByCode = new Dictionary<string, Diagnostic>(StringComparer.Ordinal);
        foreach (var finding in findings)
        {
            if (!firstByCode.ContainsKey(finding.Diagnostic.Code))
            {
                firstByCode[finding.Diagnostic.Code] = finding.Diagnostic;
            }
        }

        var rules = new JsonArray();
        foreach (var code in codes)
        {
            var example = firstByCode[code];

            rules.Add(new JsonObject
            {
                ["id"] = code,
                ["name"] = code,
                ["shortDescription"] = new JsonObject { ["text"] = example.Message },
                ["fullDescription"] = new JsonObject
                {
                    ["text"] = example.Suggestion is { Length: > 0 } suggestion
                        ? $"{example.Message} {suggestion}"
                        : example.Message,
                },
                ["defaultConfiguration"] = new JsonObject { ["level"] = ToLevel(example.Severity) },
                ["helpUri"] = $"{SkillForgeTool.InformationUri}/blob/main/docs/validation-rules.md",
            });
        }

        var results = new JsonArray();
        foreach (var (skillPath, diagnostic) in findings)
        {
            var result = new JsonObject
            {
                ["ruleId"] = diagnostic.Code,
                ["ruleIndex"] = ruleIndex[diagnostic.Code],
                ["level"] = ToLevel(diagnostic.Severity),
                ["message"] = new JsonObject
                {
                    ["text"] = diagnostic.Suggestion is { Length: > 0 } suggestion
                        ? $"{diagnostic.Message} {suggestion}"
                        : diagnostic.Message,
                },
            };

            if (diagnostic.FilePath is { Length: > 0 } filePath)
            {
                result["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["physicalLocation"] = new JsonObject
                        {
                            ["artifactLocation"] = new JsonObject
                            {
                                ["uri"] = ToUri(skillPath, filePath),
                            },
                            ["region"] = new JsonObject
                            {
                                // SARIF requires a positive line number; findings about a whole file
                                // are anchored at line 1 so the annotation still lands on the file.
                                ["startLine"] = diagnostic.Line is > 0 ? diagnostic.Line : 1,
                            },
                        },
                    },
                };
            }

            results.Add(result);
        }

        var document = new JsonObject
        {
            ["$schema"] = SarifSchema,
            ["version"] = SarifVersion,
            ["runs"] = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"] = SkillForgeTool.Name,
                            ["version"] = SkillForgeTool.Version,
                            ["informationUri"] = SkillForgeTool.InformationUri,
                            ["rules"] = rules,
                        },
                    },
                    ["results"] = results,
                },
            },
        };

        return document.ToJsonString(WriterOptions) + Environment.NewLine;
    }

    /// <summary>
    /// Builds a repository-relative URI. GitHub matches annotations by path, so the skill directory is
    /// expressed relative to the working directory rather than as an absolute agent path.
    /// </summary>
    private static string ToUri(string skillPath, string filePath)
    {
        var relativeSkillPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), skillPath);

        // If the skill lives outside the working directory there is nothing sensible to be relative to;
        // fall back to the file name alone rather than emitting a path GitHub cannot match.
        if (relativeSkillPath.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeSkillPath))
        {
            return filePath.Replace('\\', '/');
        }

        return Path.Combine(relativeSkillPath, filePath).Replace('\\', '/');
    }

    private static string ToLevel(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "note",
    };
}
