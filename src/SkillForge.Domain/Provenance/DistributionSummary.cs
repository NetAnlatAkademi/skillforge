namespace SkillForge.Domain.Provenance;

/// <summary>
/// The distribution shape of everything found, counted.
/// </summary>
/// <remarks>
/// Counts only what was observed. There is no "manually updated" count, because nothing on disk states that: an
/// asset whose distribution says nothing about updates lands in <see cref="UnknownProvenance"/>, which is the
/// honest place for it.
/// </remarks>
/// <param name="Marketplaces">Distinct marketplaces the assets are listed in.</param>
/// <param name="AutomaticUpdates">Assets whose distribution declares that it updates them by itself.</param>
/// <param name="FloatingAssets">Assets whose source names something that can move.</param>
/// <param name="PinnedAssets">Assets whose source names an immutable revision.</param>
/// <param name="UnknownProvenance">Assets nothing observed can trace to a source.</param>
public sealed record DistributionSummary(
    int Marketplaces,
    int AutomaticUpdates,
    int FloatingAssets,
    int PinnedAssets,
    int UnknownProvenance)
{
    /// <summary>Counts the distribution shape of a set of assets.</summary>
    /// <param name="assets">Assets to count.</param>
    /// <returns>The summary.</returns>
    public static DistributionSummary From(IReadOnlyList<AssetProvenance> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return new DistributionSummary(
            assets.Select(asset => asset.Source.Marketplace)
                .Where(marketplace => marketplace is { Length: > 0 })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            assets.Count(asset => asset.Source.UpdateMode == UpdateMode.Automatic),
            assets.Count(asset => asset.Source.UpdateMode == UpdateMode.Floating),
            assets.Count(asset => asset.Source.UpdateMode == UpdateMode.Pinned),
            assets.Count(asset => asset.Status == ProvenanceStatus.Unknown));
    }
}
