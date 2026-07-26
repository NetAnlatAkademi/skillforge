using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that every file the body links to exists in the skill.
/// </summary>
/// <remarks>
/// Comparison is case-sensitive on every platform. A skill that writes <c>References/Notes.md</c> for a
/// file named <c>references/notes.md</c> works on Windows and breaks on Linux, and reporting that
/// everywhere is the point: the diagnostic names a real portability bug rather than hiding it until
/// somebody runs the skill on a different machine.
/// </remarks>
public sealed class ReferencedFileExistsRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ReferencedFileNotFound;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var present = skill.Resources
            .Select(resource => resource.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        var diagnostics = new List<Diagnostic>();
        var alreadyReported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in MarkdownLinkExtractor.Extract(skill.Body, skill.BodyStartLine))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = SkillRelativePath.Normalise(link.Target);

            // A reference pointing outside the skill is SF0008's business, not this rule's.
            if (target is null || present.Contains(target))
            {
                continue;
            }

            // The same missing file mentioned twice is one problem. Report it where it first appears.
            if (!alreadyReported.Add(target))
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Error(
                Code,
                $"The referenced file '{target}' does not exist in the skill.",
                SkillDefinition.SkillFileName,
                link.Line,
                $"Add '{target}' to the skill, or correct the link. "
                    + "Paths are compared case-sensitively so the skill behaves the same on Linux."));
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
