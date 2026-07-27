using SkillForge.Domain.Validation;

namespace SkillForge.Application.Abstractions;

/// <summary>
/// Turns a validation report into machine-readable text.
/// </summary>
/// <remarks>
/// One implementation per <see cref="Format"/>, resolved by name so adding a format does not mean touching
/// the command classes.
/// </remarks>
public interface IValidationReportSerializer
{
    /// <summary>Gets the format name used on the command line, for example <c>json</c>.</summary>
    string Format { get; }

    /// <summary>Serialises a report.</summary>
    /// <param name="report">Report to serialise.</param>
    /// <returns>The serialised text, ending with a newline.</returns>
    string Serialize(ValidationReport report);

    /// <summary>Serialises a run over several skills.</summary>
    /// <param name="run">Run to serialise.</param>
    /// <returns>The serialised text, ending with a newline.</returns>
    /// <remarks>
    /// A single-skill document keeps the shape it has always had, so an existing consumer is unaffected by
    /// batches existing. What a batch looks like is each format's own decision: JSON nests the skills, while
    /// SARIF merges them into one run because that is what a code-scanning upload expects.
    /// </remarks>
    string SerializeRun(ValidationRun run);
}
