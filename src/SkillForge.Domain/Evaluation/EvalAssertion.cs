namespace SkillForge.Domain.Evaluation;

/// <summary>
/// The outcome of one assertion inside an eval case.
/// </summary>
/// <param name="Description">What was checked, phrased as the claim being made.</param>
/// <param name="Passed">Whether the claim held.</param>
/// <param name="Detail">
/// What was actually found when the claim did not hold, or extra context when it did. Never a bare "failed": a
/// reader needs to know which file was missing or which code appeared.
/// </param>
public sealed record EvalAssertion(string Description, bool Passed, string? Detail = null);
