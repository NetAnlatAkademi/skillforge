using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports body prose telling the agent to set aside or override the instructions it was given.
/// </summary>
/// <remarks>
/// The `SF4xxx` counterpart to SF3002, and the job SF3002 was measured out of. SF3002 asks whether a skill's
/// *activation text* argues with the agent; this asks whether its *instructions* do. They are separate codes
/// because they are separate problems with separate fixes, and because a body and a description need different
/// reading: this one reads prose only, so a detection pattern quoted in a code block is not a finding.
/// </remarks>
public sealed class InstructionOverrideRule : ProseInjectionRule
{
    /// <inheritdoc />
    public override string Code => DiagnosticCodes.BodyInstructionOverride;

    /// <inheritdoc />
    protected override IReadOnlyList<RiskPattern> Patterns => BodyInjectionPatterns.InstructionOverride;

    /// <inheritdoc />
    protected override string Subject =>
        "Describe the work the skill does and leave the agent's own instructions alone:";
}
