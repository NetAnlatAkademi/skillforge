using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports references that reach further than a sibling skill.
/// </summary>
/// <remarks>
/// Two or more levels up, an absolute path, or the parent directory itself: nothing about the skill's own layout
/// — or its neighbours' — can satisfy these, so they stay errors. This is the surface the SF0008 rule owns for the
/// body; the loader reports the same code for files and links on disk that escape the skill directory.
/// </remarks>
public sealed class ReferenceEscapesCollectionRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.PathEscapesSkillDirectory;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var diagnostics = new List<Diagnostic>();

        foreach (var reference in SkillReferences.Distinct(skill))
        {
            if (reference.Scope != ReferenceScope.OutsideCollection)
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Error(
                Code,
                $"The reference '{reference.Target}' reaches outside the skill and its neighbours.",
                SkillDefinition.SkillFileName,
                reference.Line,
                "Keep what a skill needs inside its own directory, or beside it. A reference that climbs further "
                    + "cannot be packaged and cannot be reviewed."));
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
