namespace SkillForge.Domain.Policy;

/// <summary>
/// What an organisation has decided its skills may do.
/// </summary>
/// <remarks>
/// Policy is the one place SkillForge makes a judgement, and it makes it only because somebody wrote the judgement
/// down. Nothing here has a default that forbids anything: a policy file that says nothing produces no findings,
/// and every rule is opt-in. That is what keeps the rest of the tool descriptive.
///
/// Every "does the policy mention this" question is a nullable rather than a default, because "allowed: false" and
/// "the policy is silent" are different decisions and only one of them is a decision.
/// </remarks>
/// <param name="Permissions">What a skill may do.</param>
/// <param name="Provenance">What must be knowable about where a skill came from.</param>
/// <param name="Skills">What a skill must declare or stay within.</param>
/// <param name="Mcp">What MCP servers may be, or <see langword="null"/> when the policy is silent about MCP.</param>
/// <param name="Suppressions">Rules this organisation has decided not to hear about, each with its reason.</param>
public sealed record PolicyDocument(
    PolicyPermissions Permissions,
    PolicyProvenance Provenance,
    PolicySkills Skills,
    PolicyMcp? Mcp,
    IReadOnlyList<PolicySuppression> Suppressions)
{
    /// <summary>A policy that decides nothing, which is what an absent or empty file means.</summary>
    public static PolicyDocument Empty { get; } = new(
        new PolicyPermissions(null, null, [], null),
        new PolicyProvenance(false, false),
        new PolicySkills(false, null),
        null,
        []);

    /// <summary>Gets the schema version the file declared.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Determines whether a code is suppressed for a skill.</summary>
    /// <param name="code">Diagnostic code being reported.</param>
    /// <param name="skillName">Skill the finding is about.</param>
    /// <returns><see langword="true"/> when the policy has recorded a reason not to report this.</returns>
    public bool Suppresses(string code, string skillName) =>
        Suppressions.Any(suppression =>
            string.Equals(suppression.Code, code, StringComparison.OrdinalIgnoreCase)
            && (suppression.Skill is null
                || string.Equals(suppression.Skill, skillName, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>What a skill may do.</summary>
/// <param name="ShellAllowed">
/// Whether shell access is allowed, or <see langword="null"/> when the policy is silent.
/// </param>
/// <param name="FilesystemWriteAllowed">
/// Whether writing to the file system is allowed, or <see langword="null"/> when the policy is silent. A policy
/// that lists paths instead of a boolean sets this to <see langword="true"/> and fills
/// <paramref name="FilesystemWritePaths"/>. The paths themselves cannot be checked against a skill, which declares
/// permissions without saying where they apply; that limit is reported rather than assumed away.
/// </param>
/// <param name="FilesystemWritePaths">Paths writing is confined to, when the policy listed any.</param>
/// <param name="AllowedDomains">
/// Hosts a skill may point at, or <see langword="null"/> when the policy is silent. An empty list is a decision
/// that no host is allowed, which is why it is not the same as <see langword="null"/>.
/// </param>
public sealed record PolicyPermissions(
    bool? ShellAllowed,
    bool? FilesystemWriteAllowed,
    IReadOnlyList<string> FilesystemWritePaths,
    IReadOnlyList<string>? AllowedDomains);

/// <summary>What must be knowable about where a skill came from.</summary>
/// <param name="RequireCommitSha">
/// Whether a skill must be traceable to a repository, a commit and a path within it, with nothing uncommitted.
/// </param>
/// <param name="RequirePackageHash">Whether a package must carry a hash.</param>
public sealed record PolicyProvenance(bool RequireCommitSha, bool RequirePackageHash);

/// <summary>What a skill must declare or stay within.</summary>
/// <param name="RequireLicense">Whether a skill must declare a license.</param>
/// <param name="MaxSkillFileLines">
/// The longest <c>SKILL.md</c> the organisation accepts, or <see langword="null"/> when it does not say.
/// </param>
public sealed record PolicySkills(bool RequireLicense, int? MaxSkillFileLines);

/// <summary>What MCP servers may be.</summary>
/// <param name="AllowedProtocolVersions">Protocol revisions the organisation accepts.</param>
/// <param name="DenyDeprecatedCapabilities">Whether a deprecated capability is a violation.</param>
public sealed record PolicyMcp(
    IReadOnlyList<string> AllowedProtocolVersions,
    bool DenyDeprecatedCapabilities);

/// <summary>A rule this organisation has decided not to hear about.</summary>
/// <param name="Code">The diagnostic code being silenced.</param>
/// <param name="Skill">The skill it applies to, or <see langword="null"/> for every skill.</param>
/// <param name="Reason">Why. Required: a suppression with no reason is refused rather than applied.</param>
public sealed record PolicySuppression(string Code, string? Skill, string Reason);
