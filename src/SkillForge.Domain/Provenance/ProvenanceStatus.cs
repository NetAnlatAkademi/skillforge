namespace SkillForge.Domain.Provenance;

/// <summary>
/// What could be established about an asset's relationship to where it came from.
/// </summary>
/// <remarks>
/// A status, not a verdict, and never a guess: <see cref="Unknown"/> is a real answer and the most common one for a
/// skill copied out of a repository by hand. The four drift values are only ever produced by
/// <c>provenance diff</c>, because a single snapshot has nothing to have drifted from.
/// </remarks>
public enum ProvenanceStatus
{
    /// <summary>Nothing observed identifies where this came from.</summary>
    Unknown,

    /// <summary>
    /// A repository, a commit and a clean working tree. Named "verified source" because the source is identified,
    /// not because anything was cryptographically verified — recorded provenance is not a signature.
    /// </summary>
    VerifiedSource,

    /// <summary>The asset declares an upstream it is not itself.</summary>
    Forked,

    /// <summary>Content changed against the revision it names.</summary>
    Drifted,

    /// <summary>The working copy has uncommitted changes, so the commit it names is not what is here.</summary>
    Modified,

    /// <summary>The source names nothing immutable, so what arrives next is not decided by anyone reviewing it.</summary>
    Unpinned,

    /// <summary>The publisher is not the one recorded earlier.</summary>
    PublisherChanged,

    /// <summary>The marketplace is not the one recorded earlier.</summary>
    MarketplaceChanged,
}
