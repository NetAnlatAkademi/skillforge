using SkillForge.Domain.Diffing;

namespace SkillForge.Domain.Provenance;

/// <summary>
/// What changed about where two trees' assets come from.
/// </summary>
/// <param name="BeforePath">The earlier tree.</param>
/// <param name="AfterPath">The later tree.</param>
/// <param name="Changed">Assets present in both, with something different about their distribution.</param>
/// <param name="Added">Assets only the later tree has.</param>
/// <param name="Removed">Assets only the earlier tree has.</param>
public sealed record ProvenanceDiff(
    string BeforePath,
    string AfterPath,
    IReadOnlyList<AssetProvenanceChange> Changed,
    IReadOnlyList<AssetProvenance> Added,
    IReadOnlyList<AssetProvenance> Removed)
{
    /// <summary>Gets a value indicating whether anything at all is different.</summary>
    public bool HasChanges => Changed.Count > 0 || Added.Count > 0 || Removed.Count > 0;
}

/// <summary>
/// One asset, and what changed about where it comes from.
/// </summary>
/// <remarks>
/// Every field is a <see cref="SurfaceValueChange"/> that is <see langword="null"/> when nothing changed, so the
/// consumer never has to compare the two sides itself — and so a report can be written by listing the fields that
/// are not null.
/// </remarks>
/// <param name="Name">The asset's name in the later tree.</param>
/// <param name="Kind">Whether it is a skill or a plugin.</param>
/// <param name="Path">Its path in the later tree.</param>
/// <param name="Before">What the earlier tree said about it.</param>
/// <param name="After">What the later tree says about it.</param>
/// <param name="Publisher">Publisher change.</param>
/// <param name="Marketplace">Marketplace change.</param>
/// <param name="Repository">Repository change.</param>
/// <param name="Version">Declared version change.</param>
/// <param name="Commit">Commit change.</param>
/// <param name="Fingerprint">Content fingerprint change.</param>
/// <param name="UpdateMode">Update mode change.</param>
/// <param name="Upstream">Declared upstream change.</param>
/// <param name="Modifications">
/// Change in how many of the asset's files are uncommitted. Compared because a working copy that started differing
/// from the revision it names is a change in what is on disk, and an asset whose only movement is that one would
/// otherwise not appear in the diff at all.
/// </param>
public sealed record AssetProvenanceChange(
    string Name,
    AssetKind Kind,
    string Path,
    AssetProvenance Before,
    AssetProvenance After,
    SurfaceValueChange? Publisher,
    SurfaceValueChange? Marketplace,
    SurfaceValueChange? Repository,
    SurfaceValueChange? Version,
    SurfaceValueChange? Commit,
    SurfaceValueChange? Fingerprint,
    SurfaceValueChange? UpdateMode,
    SurfaceValueChange? Upstream,
    SurfaceValueChange? Modifications)
{
    /// <summary>Gets a value indicating whether anything about this asset's distribution changed.</summary>
    public bool HasChanges =>
        Publisher is not null
        || Marketplace is not null
        || Repository is not null
        || Version is not null
        || Commit is not null
        || Fingerprint is not null
        || UpdateMode is not null
        || Upstream is not null
        || Modifications is not null;
}
