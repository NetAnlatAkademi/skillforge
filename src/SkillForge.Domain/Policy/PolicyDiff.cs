using SkillForge.Domain.Diffing;

namespace SkillForge.Domain.Policy;

/// <summary>
/// How one policy file differs from another.
/// </summary>
/// <remarks>
/// A policy is code that decides whether other code ships, and it is reviewed in the same pull requests as
/// everything else — where a two-character edit to a wildcard reads as a two-character edit. This is the delta a
/// reviewer actually needs: which rules were relaxed.
///
/// Pure data, in both directions. A rule being tightened is recorded here exactly as a rule being relaxed is; which
/// of them is a finding is decided by <c>PolicyChangeDiagnostics</c>, so this record can be printed in full without
/// implying that everything in it is a problem.
/// </remarks>
/// <param name="BeforePath">The earlier policy file.</param>
/// <param name="AfterPath">The later policy file.</param>
/// <param name="ShellAllowed">Whether skills may run shell commands, when it changed.</param>
/// <param name="FilesystemWriteAllowed">Whether skills may write to the file system, when it changed.</param>
/// <param name="FilesystemWritePaths">Paths writing is confined to, added and removed.</param>
/// <param name="AllowedDomainsDeclared">
/// Whether the host allow-list exists at all, when that changed. A list that disappears stops a check running,
/// which is not the same edit as a host being added to it and does not look like one in a patch either.
/// </param>
/// <param name="AllowedDomains">Hosts skills may point at, added and removed.</param>
/// <param name="RequireCommitSha">Whether provenance is required, when it changed.</param>
/// <param name="RequirePackageHash">Whether a package hash is required, when it changed.</param>
/// <param name="RequireLicense">Whether a license is required, when it changed.</param>
/// <param name="MaxSkillFileLines">The accepted <c>SKILL.md</c> length, when it changed.</param>
/// <param name="Suppressions">Silenced rules, added and removed, as <c>code</c> or <c>code (skill)</c>.</param>
/// <param name="Mcp">How the <c>mcp</c> section changed.</param>
public sealed record PolicyDiff(
    string BeforePath,
    string AfterPath,
    SurfaceValueChange? ShellAllowed,
    SurfaceValueChange? FilesystemWriteAllowed,
    SurfaceSetDiff FilesystemWritePaths,
    SurfaceValueChange? AllowedDomainsDeclared,
    SurfaceSetDiff AllowedDomains,
    SurfaceValueChange? RequireCommitSha,
    SurfaceValueChange? RequirePackageHash,
    SurfaceValueChange? RequireLicense,
    SurfaceValueChange? MaxSkillFileLines,
    SurfaceSetDiff Suppressions,
    McpPolicyDiff Mcp)
{
    /// <summary>Gets a value indicating whether the two policies decide anything differently.</summary>
    public bool HasChanges =>
        ShellAllowed is not null
        || FilesystemWriteAllowed is not null
        || FilesystemWritePaths.HasChanges
        || AllowedDomainsDeclared is not null
        || AllowedDomains.HasChanges
        || RequireCommitSha is not null
        || RequirePackageHash is not null
        || RequireLicense is not null
        || MaxSkillFileLines is not null
        || Suppressions.HasChanges
        || Mcp.HasChanges;
}
