using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Migration;

/// <summary>
/// What one adapter found for its provider.
/// </summary>
/// <param name="Presence">Whether the provider was found, and which of its files exist.</param>
/// <param name="Skills">Skills installed for this provider.</param>
/// <param name="McpServers">MCP servers this provider's configuration declares.</param>
/// <param name="InstructionFiles">Instruction files this provider reads.</param>
/// <param name="Diagnostics">What the adapter could not read, and why.</param>
public sealed record AgentToolScan(
    AgentToolPresence Presence,
    IReadOnlyList<SkillInventoryEntry> Skills,
    IReadOnlyList<McpServerDeclaration> McpServers,
    IReadOnlyList<InstructionFileReference> InstructionFiles,
    IReadOnlyList<Diagnostic> Diagnostics);
