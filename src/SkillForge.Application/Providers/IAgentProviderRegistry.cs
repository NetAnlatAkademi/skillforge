using SkillForge.Domain.Providers;

namespace SkillForge.Application.Providers;

/// <summary>
/// The providers SkillForge recognises.
/// </summary>
/// <remarks>
/// An interface rather than a static list because it is the seam the v0.4 work needs: <c>migrate inspect</c>
/// asks the same question ("which providers do we know about, and what do we know?") of a wider set, and MCP
/// adapters will sit beside these profiles rather than inside the rules.
/// </remarks>
public interface IAgentProviderRegistry
{
    /// <summary>Gets every known profile, ordered by identifier so output is reproducible.</summary>
    IReadOnlyList<AgentProviderProfile> Profiles { get; }

    /// <summary>
    /// Finds the profile for an identifier.
    /// </summary>
    /// <param name="id">Identifier as written in the skill, matched case-insensitively after trimming.</param>
    /// <returns>The profile, or <see langword="null"/> when the identifier is not one SkillForge knows.</returns>
    AgentProviderProfile? Find(string id);

    /// <summary>
    /// Suggests the known identifier an unrecognised one was most likely meant to be.
    /// </summary>
    /// <param name="id">The unrecognised identifier.</param>
    /// <returns>
    /// A known identifier, or <see langword="null"/> when nothing is close enough to name without guessing.
    /// </returns>
    string? Suggest(string id);
}
