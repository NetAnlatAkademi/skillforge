using SkillForge.Domain;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Turns the raw text of a frontmatter block into a <see cref="SkillFrontmatter"/>.
/// </summary>
/// <remarks>
/// Implemented in the Infrastructure layer so that the Application layer never references a YAML
/// library. Implementations must not throw on malformed input: a parse failure is an expected outcome
/// and is reported as a diagnostic.
/// </remarks>
public interface IFrontmatterParser
{
    /// <summary>
    /// Parses a frontmatter block.
    /// </summary>
    /// <param name="yaml">The block's contents, without the <c>---</c> delimiters.</param>
    /// <param name="startLine">
    /// One-based line of the opening delimiter in the source file, used to report absolute line numbers.
    /// </param>
    /// <param name="filePath">Path reported on diagnostics, relative to the skill directory.</param>
    /// <returns>
    /// The parsed frontmatter, or a failure carrying <see cref="Domain.Diagnostics.DiagnosticCodes.FrontmatterNotParsable"/>.
    /// A successful result may still carry diagnostics, for example a duplicated field.
    /// </returns>
    OperationResult<SkillFrontmatter> Parse(string yaml, int startLine, string filePath);
}
