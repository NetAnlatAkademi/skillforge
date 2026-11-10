namespace SkillForge.Domain.Mcp.Skills;

/// <summary>
/// One manifest file: what was declared, and what arrived.
/// </summary>
/// <param name="Uri">The file's URI.</param>
/// <param name="DeclaredDigest">The digest the manifest declared.</param>
/// <param name="DeclaredSize">The size the manifest declared.</param>
/// <param name="ActualDigest">
/// <c>sha256:</c> and the hash of the bytes that arrived, or <see langword="null"/> when nothing arrived.
/// </param>
/// <param name="ActualSize">How many bytes arrived, or <see langword="null"/> when nothing did.</param>
/// <param name="Detail">Why nothing arrived, when nothing did.</param>
public sealed record McpSkillResourceCheck(
    string Uri,
    string? DeclaredDigest,
    long? DeclaredSize,
    string? ActualDigest,
    long? ActualSize,
    string? Detail = null)
{
    /// <summary>Gets a value indicating whether the file was read and both its digest and its size matched.</summary>
    public bool Matches =>
        ActualDigest is not null
        && string.Equals(DeclaredDigest, ActualDigest, StringComparison.Ordinal)
        && DeclaredSize == ActualSize;
}
