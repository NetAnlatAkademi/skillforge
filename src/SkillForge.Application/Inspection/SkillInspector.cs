using System.Text.RegularExpressions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Inspection;

/// <summary>
/// Summarises what a skill contains.
/// </summary>
/// <remarks>
/// Works entirely from the loaded model, so it needs no file system of its own. URLs are read from the
/// skill's entry point only — reading every referenced file would be a fuller answer, and is the kind of
/// thing the security-signals milestone is for; what this does today is stated plainly in the output rather
/// than implied to be exhaustive.
/// </remarks>
public sealed partial class SkillInspector : ISkillInspector
{
    /// <inheritdoc />
    public ValueTask<SkillInspection> InspectAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        cancellationToken.ThrowIfCancellationRequested();

        var scripts = skill.Resources.Where(resource => resource.Kind == SkillResourceKind.Script).ToArray();
        var binaries = skill.Resources.Where(resource => resource.Kind == SkillResourceKind.Binary).ToArray();
        var hasEvals = skill.Resources.Any(resource =>
            resource.RelativePath.StartsWith("evals/", StringComparison.OrdinalIgnoreCase));

        var urls = ExternalUrlPattern()
            .Matches(skill.Body)
            .Select(match => match.Value.TrimEnd('.', ',', ')', '"', '\''))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var capabilities = new List<string> { SkillCapabilities.FilesystemRead };
        if (scripts.Length > 0)
        {
            capabilities.Add(SkillCapabilities.ShellExecution);
        }

        if (urls.Length > 0)
        {
            capabilities.Add(SkillCapabilities.NetworkAccess);
        }

        if (binaries.Length > 0)
        {
            capabilities.Add(SkillCapabilities.BinaryContent);
        }

        var diagnostics = new List<Diagnostic>();

        foreach (var script in scripts)
        {
            diagnostics.Add(Diagnostic.Info(
                DiagnosticCodes.ContainsScript,
                $"The skill contains a script: {script.RelativePath}",
                script.RelativePath));
        }

        foreach (var url in urls)
        {
            diagnostics.Add(Diagnostic.Info(
                DiagnosticCodes.ContainsExternalUrl,
                $"The skill references an external URL: {url}",
                SkillDefinition.SkillFileName));
        }

        foreach (var binary in binaries)
        {
            diagnostics.Add(Diagnostic.Info(
                DiagnosticCodes.ContainsBinaryFile,
                $"The skill contains a binary file: {binary.RelativePath}",
                binary.RelativePath));
        }

        if (hasEvals)
        {
            diagnostics.Add(Diagnostic.Info(
                DiagnosticCodes.ContainsEvals,
                "The skill contains an evals folder.",
                "evals"));
        }

        var inspection = new SkillInspection(
            skill.Name,
            skill.DirectoryPath,
            skill.Frontmatter.Version,
            skill.Resources,
            urls,
            capabilities,
            skill.Frontmatter.AllowedTools,
            diagnostics);

        return ValueTask.FromResult(inspection);
    }

    /// <summary>Matches an http or https URL up to the first whitespace or closing bracket.</summary>
    [GeneratedRegex(@"https?://[^\s)\]<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalUrlPattern();
}
