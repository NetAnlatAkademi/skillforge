namespace SkillForge.Application.Abstractions;

/// <summary>
/// Finds the skills inside a directory that holds several of them.
/// </summary>
public interface ISkillDiscovery
{
    /// <summary>
    /// Lists the skill directories under a root.
    /// </summary>
    /// <param name="rootDirectory">Directory to search.</param>
    /// <returns>
    /// Absolute paths of the directories that contain a <c>SKILL.md</c>, ordered so a run over unchanged
    /// input reports the same skills in the same sequence. Empty when the root holds no skills.
    /// </returns>
    IReadOnlyList<string> FindSkillDirectories(string rootDirectory);
}
