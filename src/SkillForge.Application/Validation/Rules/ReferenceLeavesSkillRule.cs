using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports references that point at a sibling of this skill.
/// </summary>
/// <remarks>
/// A warning, not an error. Skills that live together and reference each other are a normal pattern — measured
/// on 229 real skills, treating this as an error produced 21 findings that were all deliberate cross-references.
/// What is still worth saying is that such a reference cannot be satisfied by this skill alone, so packaging or
/// sharing it in isolation leaves a dangling link.
/// </remarks>
public sealed class ReferenceLeavesSkillRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ReferenceLeavesSkill;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var diagnostics = new List<Diagnostic>();

        foreach (var reference in SkillReferences.Distinct(skill))
        {
            if (reference.Scope != ReferenceScope.SiblingSkill)
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Warning(
                Code,
                $"The reference '{reference.Target}' points at a sibling skill "
                    + $"('{reference.SiblingName}'), outside this skill's own directory.",
                SkillDefinition.SkillFileName,
                reference.Line,
                "Fine inside a collection of skills that ship together. If this skill is meant to stand alone, "
                    + "copy what it needs into its own directory instead."));
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
