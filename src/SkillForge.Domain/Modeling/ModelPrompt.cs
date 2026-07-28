namespace SkillForge.Domain.Modeling;

/// <summary>
/// One question put to a model.
/// </summary>
/// <param name="System">Instructions describing the task and the answer format.</param>
/// <param name="User">The user turn — for activation probing, the prompt a person might actually type.</param>
public sealed record ModelPrompt(string System, string User);
