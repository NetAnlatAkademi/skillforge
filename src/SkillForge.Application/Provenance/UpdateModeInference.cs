using System.Text.RegularExpressions;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Decides what a marketplace entry implies about how a plugin updates.
/// </summary>
/// <remarks>
/// Separate from the reader that produced the entry, so the decision can be argued with — and tested — without a
/// JSON file. Every branch here is driven by something the entry actually says. An entry that names no revision and
/// declares nothing is <see cref="UpdateMode.Unknown"/> rather than pinned, because "nobody wrote it down" is the
/// answer, and the alternative would report the safest possible mode for the least evidence.
/// </remarks>
public static partial class UpdateModeInference
{
    /// <summary>Revision names that move by design, whatever repository they are in.</summary>
    private static readonly string[] MovingRevisions =
        ["main", "master", "head", "latest", "trunk", "develop", "dev", "stable", "*"];

    /// <summary>Works out how a plugin updates, from what its marketplace entry says.</summary>
    /// <param name="entry">The entry, or <see langword="null"/> when no marketplace lists the plugin.</param>
    /// <returns>The mode the entry implies.</returns>
    public static UpdateMode From(MarketplaceEntry? entry)
    {
        if (entry is null)
        {
            return UpdateMode.Unknown;
        }

        // An explicit statement wins over anything inferred from a revision string: the entry is saying what it
        // does, and a commit SHA next to "autoUpdate: true" means the SHA is what it updates *from*.
        if (entry.AutomaticUpdate is true)
        {
            return UpdateMode.Automatic;
        }

        if (entry.DeclaredUpdateMode is { Length: > 0 } declared
            && Enum.TryParse<UpdateMode>(declared, ignoreCase: true, out var mode)
            && mode != UpdateMode.Unknown)
        {
            return mode;
        }

        if (entry.Revision is not { Length: > 0 } revision)
        {
            // No revision at all still floats when a repository is named: whatever that repository's default
            // branch holds at install time is what arrives.
            return entry.RepositoryUrl is { Length: > 0 } ? UpdateMode.Floating : UpdateMode.Unknown;
        }

        if (MovingRevisions.Contains(revision, StringComparer.OrdinalIgnoreCase))
        {
            return UpdateMode.Floating;
        }

        return CommitSha().IsMatch(revision) || VersionTag().IsMatch(revision)
            ? UpdateMode.Pinned
            : UpdateMode.Floating;
    }

    /// <summary>An abbreviated or full git object name.</summary>
    [GeneratedRegex("^[0-9a-f]{7,40}$", RegexOptions.IgnoreCase)]
    private static partial Regex CommitSha();

    /// <summary>
    /// A version tag. Treated as pinned even though a tag can be moved: republishing a tag is a supply-chain event
    /// in itself, and reporting every tagged install as floating would bury the entries that genuinely are.
    /// </summary>
    [GeneratedRegex(@"^v?\d+(\.\d+)*([-+][0-9A-Za-z.-]+)?$")]
    private static partial Regex VersionTag();
}
