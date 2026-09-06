using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Provenance;

namespace SkillForge.Domain.Updates;

/// <summary>
/// How risky one update is, and why.
/// </summary>
/// <remarks>
/// The scale is short on purpose. Everything below <see cref="High"/> is "here is what changed"; <c>High</c> and
/// <c>Critical</c> are the two states worth interrupting somebody for, and a scale with more rungs than that ends
/// up with every finding parked in the middle.
/// </remarks>
public enum UpdateRisk
{
    /// <summary>Nothing changed that lets the asset reach further.</summary>
    Informational,

    /// <summary>The asset reaches further, and the update was pinned — somebody chose to take it.</summary>
    Medium,

    /// <summary>The asset reaches further and the update arrives without review.</summary>
    High,

    /// <summary>The update arrives without review and from somebody other than the recorded publisher.</summary>
    Critical,
}

/// <summary>
/// What an update does to what an asset can reach, and how that combines with how it arrives.
/// </summary>
/// <remarks>
/// The combination is the point. A new shell script is ordinary in a release somebody chose to install and is a
/// different thing entirely in a plugin that updates itself overnight — the capability diff and the update mode
/// are each half of one answer, which is why this type carries both rather than reporting them separately.
/// </remarks>
/// <param name="BasePath">The tree the update starts from.</param>
/// <param name="TargetPath">The tree it arrives at.</param>
/// <param name="UpdateMode">How the update arrives, as far as the distribution says.</param>
/// <param name="Provenance">What changed about where the assets come from.</param>
/// <param name="Capabilities">What the target can reach that the base could not.</param>
/// <param name="Risk">The combined verdict.</param>
/// <param name="Reason">One sentence saying which combination produced that verdict.</param>
/// <param name="Findings">The diagnostics, in the standard report order.</param>
public sealed record UpdateAnalysis(
    string BasePath,
    string TargetPath,
    UpdateMode UpdateMode,
    ProvenanceDiff Provenance,
    CapabilitySurfaceDiff Capabilities,
    UpdateRisk Risk,
    string Reason,
    IReadOnlyList<Diagnostic> Findings);
