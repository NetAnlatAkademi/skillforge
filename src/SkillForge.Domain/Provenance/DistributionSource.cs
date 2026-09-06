namespace SkillForge.Domain.Provenance;

/// <summary>
/// Where an asset is distributed from, as far as its own files say.
/// </summary>
/// <remarks>
/// Every field except <see cref="Sha256"/> and <see cref="UpdateMode"/> is nullable, and that is the type's main
/// claim: a skill copied into a repository by hand has no publisher and no marketplace, and filling either in
/// would produce provenance a consumer would believe. <see cref="Sha256"/> is always computable because it is a
/// fact about the bytes on disk rather than a claim about their origin.
///
/// It is not a signature and it is not verified. It records what a manifest, a marketplace entry and a git
/// checkout said at the time SkillForge looked.
/// </remarks>
/// <param name="Marketplace">Marketplace the asset is listed in, or <see langword="null"/> when none lists it.</param>
/// <param name="Publisher">Publisher as the manifest or marketplace names them.</param>
/// <param name="RepositoryUrl">Repository the asset is distributed from.</param>
/// <param name="Version">Version the asset declares.</param>
/// <param name="CommitSha">Commit the working copy is at, when it is in a repository.</param>
/// <param name="Sha256">
/// Fingerprint of the asset's own files: the same bytes on any machine produce the same value, which is what makes
/// "this is not the copy that was reviewed" a computable statement.
/// </param>
/// <param name="UpdateMode">How the asset gets its next version, when the distribution says.</param>
public sealed record DistributionSource(
    string? Marketplace,
    string? Publisher,
    string? RepositoryUrl,
    string? Version,
    string? CommitSha,
    string? Sha256,
    UpdateMode UpdateMode)
{
    /// <summary>Gets a source that says nothing, which is what an asset outside any distribution has.</summary>
    public static DistributionSource Unattributed { get; } =
        new(null, null, null, null, null, null, UpdateMode.Unknown);

    /// <summary>
    /// Gets a value indicating whether the asset can be traced to a repository at a named revision.
    /// </summary>
    public bool IdentifiesItsSource =>
        RepositoryUrl is { Length: > 0 } && CommitSha is { Length: > 0 };
}
