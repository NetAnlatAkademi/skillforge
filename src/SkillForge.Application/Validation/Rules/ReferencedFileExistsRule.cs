using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that every file the body links to exists in the skill.
/// </summary>
/// <remarks>
/// Only references that stay inside the skill are checked here: a sibling or further-out reference is somebody
/// else's finding (SF1011, SF0008), and this rule has no way to look outside the skill's own inventory anyway.
///
/// Comparison is case-sensitive on every platform. A skill that writes <c>References/Notes.md</c> for a file named
/// <c>references/notes.md</c> works on Windows and breaks on Linux, and reporting that everywhere is the point: the
/// diagnostic names a real portability bug rather than hiding it until somebody runs the skill on another machine.
/// </remarks>
public sealed class ReferencedFileExistsRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ReferencedFileNotFound;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var present = skill.Resources
            .Select(resource => resource.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        var diagnostics = new List<Diagnostic>();

        foreach (var reference in SkillReferences.Distinct(skill))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reference.PathInsideSkill is not { Length: > 0 } target || present.Contains(target))
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Error(
                Code,
                $"The referenced file '{target}' does not exist in the skill.",
                SkillDefinition.SkillFileName,
                reference.Line,
                $"Add '{target}' to the skill, or correct the link. "
                    + "Paths are compared case-sensitively so the skill behaves the same on Linux."));
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
