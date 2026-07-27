using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Diffing;

/// <summary>
/// How one version of a skill differs from another in what it can do.
/// </summary>
/// <remarks>
/// This is a behaviour-surface diff, not a file diff: git already shows which bytes changed, and what a reviewer
/// cannot see from a patch is that a skill quietly gained a permission, a script or a new domain to talk to.
///
/// What this deliberately does **not** claim is whether the activation scope became "broader". Judging that from
/// a description's text is not something we can do honestly — a shorter description can match more, a longer one
/// can match more, and the words that matter depend on the agent. It reports that the description changed and
/// shows both, which a human can judge. Actually testing activation is what evals are for.
/// </remarks>
/// <param name="BeforePath">Path of the earlier version.</param>
/// <param name="AfterPath">Path of the later version.</param>
/// <param name="Name">The skill's name before and after, when it changed.</param>
/// <param name="Version">The declared version before and after, when it changed.</param>
/// <param name="Description">The description before and after, when it changed.</param>
/// <param name="DeclaredTools">Tools declared under <c>allowed-tools</c>.</param>
/// <param name="Compatibility">Agents declared under <c>compatibility</c>.</param>
/// <param name="ExternalDomains">Hosts the skill's entry point points at.</param>
/// <param name="Scripts">Executable files the skill ships.</param>
/// <param name="Files">Every file the skill ships.</param>
/// <param name="NewFindings">Diagnostics present after but not before.</param>
/// <param name="ResolvedFindings">Diagnostics present before but not after.</param>
public sealed record SkillSurfaceDiff(
    string BeforePath,
    string AfterPath,
    SurfaceValueChange? Name,
    SurfaceValueChange? Version,
    SurfaceValueChange? Description,
    SurfaceSetDiff DeclaredTools,
    SurfaceSetDiff Compatibility,
    SurfaceSetDiff ExternalDomains,
    SurfaceSetDiff Scripts,
    SurfaceSetDiff Files,
    IReadOnlyList<Diagnostic> NewFindings,
    IReadOnlyList<Diagnostic> ResolvedFindings)
{
    /// <summary>Gets a value indicating whether anything about the surface changed at all.</summary>
    public bool HasChanges =>
        Name is not null
        || Version is not null
        || Description is not null
        || DeclaredTools.HasChanges
        || Compatibility.HasChanges
        || ExternalDomains.HasChanges
        || Scripts.HasChanges
        || Files.HasChanges
        || NewFindings.Count > 0
        || ResolvedFindings.Count > 0;

    /// <summary>
    /// Gets the changes that widen what the skill can do, which is what a reviewer should look at first.
    /// </summary>
    /// <remarks>
    /// A new permission, a new script or a new domain are the three ways a skill's reach grows. They are grouped
    /// because a pull request comment has room for one line that matters, not eight that might.
    /// </remarks>
    public bool ReachGrew =>
        DeclaredTools.Added.Count > 0 || Scripts.Added.Count > 0 || ExternalDomains.Added.Count > 0;

    /// <summary>Gets the new findings that are errors, which is what makes a diff a regression.</summary>
    public IReadOnlyList<Diagnostic> NewErrors =>
        [.. NewFindings.Where(finding => finding.Severity == DiagnosticSeverity.Error)];
}
