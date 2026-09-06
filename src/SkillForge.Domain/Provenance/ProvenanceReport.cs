using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Provenance;

/// <summary>
/// What <c>provenance</c> found under one directory.
/// </summary>
/// <remarks>
/// Descriptive, like <c>inspect</c> and <c>inventory</c>: it reports where things came from and exits zero even
/// when the answer is "nothing here can be traced anywhere". Whether unknown provenance is acceptable is a policy
/// decision, and policy is where SkillForge makes those.
/// </remarks>
/// <param name="Root">Directory that was scanned.</param>
/// <param name="Repository">Repository the directory belongs to, or <see langword="null"/> when it is not in one.</param>
/// <param name="Commit">Commit the checkout is at, or <see langword="null"/>.</param>
/// <param name="Assets">Assets found, ordered by path.</param>
/// <param name="Diagnostics">What SkillForge could not read, and why.</param>
public sealed record ProvenanceReport(
    string Root,
    string? Repository,
    string? Commit,
    IReadOnlyList<AssetProvenance> Assets,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Gets the distribution shape of the assets found.</summary>
    public DistributionSummary Distribution => DistributionSummary.From(Assets);
}
