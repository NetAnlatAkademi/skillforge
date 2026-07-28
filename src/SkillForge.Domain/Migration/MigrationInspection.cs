using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Migration;

/// <summary>
/// Everything <c>migrate inspect</c> found, across every provider it knows.
/// </summary>
/// <remarks>
/// Descriptive, in the same sense as <c>inspect</c> and for the same reason (ADR-006): it reports what is
/// installed and how it is configured, and it does not decide whether any of that is a problem. The diagnostics
/// it carries are about SkillForge's own reading — a configuration file it could not parse — not judgements about
/// the setup.
/// </remarks>
/// <param name="UserDirectory">The home directory the user-scoped configuration was read from.</param>
/// <param name="ProjectDirectory">The project directory that was inspected, or <see langword="null"/>.</param>
/// <param name="Providers">Every provider SkillForge looked for, present or not, ordered by identifier.</param>
/// <param name="Skills">Skills found across all providers.</param>
/// <param name="McpServers">MCP servers declared across all providers.</param>
/// <param name="InstructionFiles">Instruction files in play across all providers.</param>
/// <param name="Diagnostics">
/// What SkillForge could not read, and why. A configuration it failed to parse is reported rather than skipped
/// silently, because a missing server in an inventory is worse than a stated gap.
/// </param>
public sealed record MigrationInspection(
    string UserDirectory,
    string? ProjectDirectory,
    IReadOnlyList<AgentToolPresence> Providers,
    IReadOnlyList<SkillInventoryEntry> Skills,
    IReadOnlyList<McpServerDeclaration> McpServers,
    IReadOnlyList<InstructionFileReference> InstructionFiles,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Gets the providers that were actually found.</summary>
    public IEnumerable<AgentToolPresence> PresentProviders =>
        Providers.Where(provider => provider.IsPresent);
}
