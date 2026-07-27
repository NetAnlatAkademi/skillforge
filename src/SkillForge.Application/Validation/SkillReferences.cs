using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation;

/// <summary>
/// The references in a skill's body, each classified once, with repeats dropped.
/// </summary>
/// <remarks>
/// Three rules now ask the same question of the same links — does this resolve, does it leave the skill, does it
/// leave the collection — so extracting and classifying happens once, here, rather than three times with three
/// slightly different dedupe keys. The same target mentioned twice is one finding, reported where it first appears.
/// </remarks>
public static class SkillReferences
{
    /// <summary>
    /// Extracts and classifies the local references in a skill's body.
    /// </summary>
    /// <param name="skill">Skill whose body to read.</param>
    /// <returns>One entry per distinct reference, in the order they first appear.</returns>
    public static IReadOnlyList<SkillReference> Distinct(SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var references = new List<SkillReference>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in MarkdownLinkExtractor.Extract(skill.Body, skill.BodyStartLine))
        {
            if (!seen.Add(link.Target))
            {
                continue;
            }

            var classification = SkillRelativePath.Classify(link.Target);

            references.Add(new SkillReference(
                link.Target,
                link.Line,
                classification.Scope,
                classification.PathInsideSkill,
                classification.SiblingName));
        }

        return references;
    }
}
