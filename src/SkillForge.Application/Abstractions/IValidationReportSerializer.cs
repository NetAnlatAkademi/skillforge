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
}
