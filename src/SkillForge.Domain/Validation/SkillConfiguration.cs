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

    /// <summary>
    /// Whether the skill actually ships a configuration file.
    /// </summary>
    /// <remarks>
    /// The difference between "declared nothing" and "declared it needs nothing" matters: a rule can only accuse a
    /// skill of contradicting its own declaration when there is a declaration to contradict.
    /// </remarks>
    public bool Exists { get; init; }

    /// <summary>
    /// What the skill declares under <c>permissions.network.allowed</c>, or <see langword="null"/> when it says
    /// nothing about the network.
    /// </summary>
    public bool? NetworkAllowed { get; init; }

    /// <summary>Commands the skill declares under <c>permissions.shell.allowed</c>.</summary>
    public IReadOnlyList<string> ShellAllowed { get; init; } = [];

    /// <summary>
    /// Whether the skill declares any shell permission at all — an empty list is a declaration that it needs none.
    /// </summary>
    public bool DeclaresShellPermission => ShellAllowed.Count > 0;
}
