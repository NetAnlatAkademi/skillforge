using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports body prose telling the agent to keep something from the person it is working for.
/// </summary>
/// <remarks>
/// A skill has no legitimate need for this. Whatever it does, the user is entitled to know it happened — so
/// "without telling the user" is worth a human's attention wherever it comes from, including from an author who
/// meant something innocuous by it.
///
/// The patterns are tighter than they look. "Silently" and "quietly" are everywhere in ordinary technical prose,
/// so the silence pattern also needs a verb that acts outside the skill, and the disclosure pattern needs a verb
/// of disclosure — "do not tell the user to run it twice" is about what to say, not about hiding, and does not
/// match.
/// </remarks>
public sealed class ConcealmentRule : ProseInjectionRule
{
    /// <inheritdoc />
    public override string Code => DiagnosticCodes.BodyConcealmentInstruction;

    /// <inheritdoc />
    protected override IReadOnlyList<RiskPattern> Patterns => BodyInjectionPatterns.Concealment;

    /// <inheritdoc />
    protected override string Subject => "Let the agent report what it did:";
}
