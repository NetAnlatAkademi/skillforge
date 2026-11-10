namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// One file a static skill manifest names, as the server declared it.
/// </summary>
/// <remarks>
/// Every field is what the server <strong>said</strong>. The nullable ones are nullable because a server can leave them
/// out, and a missing digest or size is never filled in: it makes the manifest invalid, which is reported rather than
/// repaired.
/// </remarks>
/// <param name="Uri">The file's URI, exactly as the manifest wrote it.</param>
/// <param name="Digest">The declared digest, for example <c>sha256:&lt;64 lowercase hex&gt;</c>.</param>
/// <param name="Size">The declared byte length of the file's raw content.</param>
public sealed record McpSkillResource(string Uri, string? Digest, long? Size);
