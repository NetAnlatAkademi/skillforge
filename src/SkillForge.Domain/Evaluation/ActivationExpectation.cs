namespace SkillForge.Domain.Evaluation;

/// <summary>
/// A prompt, and whether its wording is expected to overlap the skill's description.
/// </summary>
/// <param name="Prompt">The prompt a user might write.</param>
/// <param name="ExpectOverlap">
/// <see langword="true"/> when the description should share vocabulary with the prompt, <see langword="false"/> when
/// it should not.
/// </param>
/// <remarks>
/// **This is not an activation test, and calling it one would be a lie.** Whether an agent chooses a skill is decided
/// by a model reading a whole prompt, a whole toolbox and a whole conversation. SkillForge sends nothing to a model,
/// so it cannot answer that question and does not pretend to.
///
/// What it can answer is a **necessary condition**: an agent that never sees the skill's vocabulary in the prompt has
/// nothing to match on. A description sharing no words at all with "review my ASP.NET Core API" will not be retrieved
/// for it, whatever the model. So a failure here is informative — the skill is missing the words — while a pass proves
/// only that the skill is not disqualified on vocabulary. The report says so in those terms, and never says "would
/// fire" or "would not fire".
///
/// Real activation testing needs a model runner. That is a separate thing to build, with an honest name.
/// </remarks>
public sealed record ActivationExpectation(string Prompt, bool ExpectOverlap);
