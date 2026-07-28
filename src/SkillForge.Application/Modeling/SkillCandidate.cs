namespace SkillForge.Application.Modeling;

/// <summary>
/// A skill as it is offered to a model: the two fields an agent actually retrieves on.
/// </summary>
/// <param name="Name">The skill's name.</param>
/// <param name="Description">The skill's description.</param>
public sealed record SkillCandidate(string Name, string Description);
