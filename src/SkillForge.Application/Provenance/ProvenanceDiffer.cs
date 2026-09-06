using SkillForge.Domain.Diffing;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Compares two provenance reports.
/// </summary>
/// <remarks>
/// Pure: two reports in, one diff out, no file system and no git. That is what lets the interesting cases —
/// a publisher that changed, a pin that became a branch — be written as three lines of test each.
///
/// Assets are matched by path first and by name second. Path is the identity a reviewer reading a pull request
/// has in mind; matching the leftovers by name is what stops a moved directory from being reported as one asset
/// disappearing and an unrelated one arriving, which is the shape that hides a publisher change.
/// </remarks>
public static class ProvenanceDiffer
{
    /// <summary>Compares what two trees say about where their assets came from.</summary>
    /// <param name="before">The earlier report.</param>
    /// <param name="after">The later report.</param>
    /// <returns>The difference, with every list ordered so a report is reproducible.</returns>
    public static ProvenanceDiff Compare(ProvenanceReport before, ProvenanceReport after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var unmatchedBefore = before.Assets.ToList();
        var changed = new List<AssetProvenanceChange>();
        var added = new List<AssetProvenance>();

        foreach (var asset in after.Assets)
        {
            var match = Match(unmatchedBefore, asset);

            if (match is null)
            {
                added.Add(asset);
                continue;
            }

            unmatchedBefore.Remove(match);

            var change = Compare(match, asset);
            if (change.HasChanges)
            {
                changed.Add(change);
            }
        }

        return new ProvenanceDiff(
            before.Root,
            after.Root,
            [.. changed.OrderBy(change => change.Path, StringComparer.Ordinal)],
            [.. added.OrderBy(asset => asset.Path, StringComparer.Ordinal)],
            [.. unmatchedBefore.OrderBy(asset => asset.Path, StringComparer.Ordinal)]);
    }

    private static AssetProvenance? Match(IReadOnlyList<AssetProvenance> candidates, AssetProvenance asset) =>
        candidates.FirstOrDefault(candidate =>
            candidate.Kind == asset.Kind
            && string.Equals(candidate.Path, asset.Path, StringComparison.OrdinalIgnoreCase))
        ?? candidates.FirstOrDefault(candidate =>
            candidate.Kind == asset.Kind
            && string.Equals(candidate.Name, asset.Name, StringComparison.OrdinalIgnoreCase));

    private static AssetProvenanceChange Compare(AssetProvenance before, AssetProvenance after) => new(
        after.Name,
        after.Kind,
        after.Path,
        before,
        after,
        SurfaceValueChange.Between(before.Source.Publisher, after.Source.Publisher),
        SurfaceValueChange.Between(before.Source.Marketplace, after.Source.Marketplace),
        SurfaceValueChange.Between(before.Source.RepositoryUrl, after.Source.RepositoryUrl),
        SurfaceValueChange.Between(before.Source.Version, after.Source.Version),
        SurfaceValueChange.Between(before.Source.CommitSha, after.Source.CommitSha),
        SurfaceValueChange.Between(before.Source.Sha256, after.Source.Sha256),
        SurfaceValueChange.Between(before.Source.UpdateMode.ToString(), after.Source.UpdateMode.ToString()),
        SurfaceValueChange.Between(before.DeclaredUpstream, after.DeclaredUpstream),
        SurfaceValueChange.Between(
            Count(before.LocallyModifiedFiles),
            Count(after.LocallyModifiedFiles)));

    /// <summary>
    /// A modification count as text, so it compares like every other field. Zero is written as null: "nothing is
    /// uncommitted" is the absence of a value rather than a value of its own.
    /// </summary>
    private static string? Count(int modifiedFiles) =>
        modifiedFiles == 0 ? null : modifiedFiles.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
