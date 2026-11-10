namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// One direct child <c>resources/directory/read</c> returned.
/// </summary>
/// <param name="Uri">The child's URI.</param>
/// <param name="IsDirectory">Whether the server marked it <c>inode/directory</c>.</param>
public sealed record McpDirectoryEntry(string Uri, bool IsDirectory);
