namespace SkillForge.Domain.Migration;

/// <summary>
/// What was found of one provider's installation.
/// </summary>
/// <remarks>
/// A provider that is not installed is still reported, with <see cref="IsPresent"/> false and nothing found. The
/// absence is the answer to "can I move to it?", and leaving it out of the report would look like it was not
/// looked for.
/// </remarks>
/// <param name="ProviderId">The provider's identifier.</param>
/// <param name="DisplayName">The provider's name as a person writes it.</param>
/// <param name="IsPresent">Whether anything belonging to this provider was found.</param>
/// <param name="ConfigurationPaths">
/// The paths that were found, in the order they were looked for. Paths that do not exist are not listed.
/// </param>
public sealed record AgentToolPresence(
    string ProviderId,
    string DisplayName,
    bool IsPresent,
    IReadOnlyList<string> ConfigurationPaths);
