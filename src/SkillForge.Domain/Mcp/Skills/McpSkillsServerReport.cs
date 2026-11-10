namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// What one declared server said about the skills it serves.
/// </summary>
/// <param name="ServerName">The name the configuration gives the server.</param>
/// <param name="Endpoint">The URL asked, with credentials removed. <see langword="null"/> for a stdio server.</param>
/// <param name="DeclaresSkills">Whether the server declared the <c>io.modelcontextprotocol/skills</c> extension.</param>
/// <param name="DirectoryRead">Whether that declaration turned <c>directoryRead</c> on.</param>
/// <param name="Listing">What <c>skills/list</c> returned, when it was asked.</param>
/// <param name="Verifications">One entry per skill verified, ordered by URI. Empty under <c>inspect</c>.</param>
/// <param name="Detail">Why the server was not asked about skills, when it was not.</param>
public sealed record McpSkillsServerReport(
    string ServerName,
    string? Endpoint,
    bool DeclaresSkills,
    bool DirectoryRead,
    McpSkillListing? Listing,
    IReadOnlyList<McpSkillVerification> Verifications,
    string? Detail = null);
