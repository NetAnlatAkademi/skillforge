namespace SkillForge.Domain.Validation;

/// <summary>
/// The parts of a skill's own <c>skillforge.yaml</c> that affect validation.
/// </summary>
/// <remarks>
/// The file is optional, and its absence is not a finding — requiring it would make SkillForge's conventions a
/// condition of being a valid skill, which is the thing the two-file split exists to avoid (ADR-003).
/// </remarks>
/// <param name="Strict">Whether the skill asks for its own warnings to be treated as failures.</param>
/// <param name="SuppressedCodes">Diagnostic codes this skill has decided not to hear about.</param>
public sealed record SkillConfiguration(bool Strict, IReadOnlyList<string> SuppressedCodes)
{
    /// <summary>What applies when a skill ships no configuration file.</summary>
    public static SkillConfiguration Default { get; } = new(Strict: false, SuppressedCodes: []);
}
