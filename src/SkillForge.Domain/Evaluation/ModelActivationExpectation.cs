namespace SkillForge.Domain.Evaluation;

/// <summary>
/// Prompts a skill should and should not be chosen for, to be checked by asking a model.
/// </summary>
/// <remarks>
/// Deliberately a **separate key** from <c>activation</c> in an eval file rather than an extension of it.
/// <c>activation</c> is published and means vocabulary overlap; redefining it would change what an existing eval file
/// asserts without its author touching it. The name says where the answer comes from — a reader should never have to
/// wonder whether a result was computed or generated.
///
/// <see cref="ShouldNotFire"/> matters more than it looks. Asked in isolation, a model says yes to almost any skill
/// for almost any prompt, so a suite of positives alone measures nothing. The negative cases and the distractors are
/// what make the number mean something.
/// </remarks>
/// <param name="ShouldFire">Prompts the skill is expected to be chosen for.</param>
/// <param name="ShouldNotFire">Prompts the skill is expected not to be chosen for.</param>
/// <param name="Runs">
/// How many times to ask each prompt. More than one because a model is not deterministic even at temperature zero:
/// one answer is an anecdote, ten are a rate.
/// </param>
/// <param name="Threshold">
/// The share of runs that must agree with the expectation, from 0 to 1. Declared by the author rather than fixed by
/// SkillForge, because how reliable is reliable enough is a judgement about their skill, not ours.
/// </param>
public sealed record ModelActivationExpectation(
    IReadOnlyList<string> ShouldFire,
    IReadOnlyList<string> ShouldNotFire,
    int Runs,
    double Threshold)
{
    /// <summary>Runs to use when the file does not say.</summary>
    public const int DefaultRuns = 5;

    /// <summary>Threshold to use when the file does not say.</summary>
    public const double DefaultThreshold = 0.8;

    /// <summary>Gets the total number of prompts this expectation covers.</summary>
    public int PromptCount => ShouldFire.Count + ShouldNotFire.Count;

    /// <summary>Gets the number of model requests checking it will make.</summary>
    public int RequestCount => PromptCount * Runs;
}
