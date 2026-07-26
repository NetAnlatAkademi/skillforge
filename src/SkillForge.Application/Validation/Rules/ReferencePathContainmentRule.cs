using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that no link in the body points outside the skill directory.
/// </summary>
/// <remarks>
/// The loader reports the same code for files on disk that escape the skill. This rule covers the other
/// surface: a reference written in the Markdown body. Such a reference cannot be packaged, and pointing
/// at something like <c>../../.ssh/id_rsa</c> is worth naming plainly.
/// </remarks>
public sealed class ReferencePathContainmentRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.PathEscapesSkillDirectory;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var diagnostics = new List<Diagnostic>();
        var alreadyReported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in MarkdownLinkExtractor.Extract(skill.Body, skill.BodyStartLine))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SkillRelativePath.Normalise(link.Target) is not null
                || !alreadyReported.Add(link.Target))
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Error(
                Code,
                $"The reference '{link.Target}' points outside the skill directory.",
                SkillDefinition.SkillFileName,
                link.Line,
                "Keep everything a skill needs inside its own directory. "
                    + "A reference that escapes it cannot be packaged or reviewed."));
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }
}
