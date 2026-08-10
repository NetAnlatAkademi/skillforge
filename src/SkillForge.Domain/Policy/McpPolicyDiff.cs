using SkillForge.Domain.Diffing;

namespace SkillForge.Domain.Policy;

/// <summary>
/// How one policy's <c>mcp</c> section differs from another's.
/// </summary>
/// <remarks>
/// The question is not which lines changed — git answers that — but **what an agent may now connect to that it
/// could not before**. Two edits produce that outcome and they read very differently in a patch: an entry added to
/// <c>allow</c>, and an entry removed from <c>deny</c>. Both appear here, and
/// <see cref="NewlyPermitted"/> puts them side by side because a reviewer is asking one question, not two.
/// </remarks>
/// <param name="Default">The default decision, when it changed.</param>
/// <param name="AllowAdded">Allow rules only the later policy carries.</param>
/// <param name="AllowRemoved">Allow rules only the earlier one carries.</param>
/// <param name="DenyAdded">Deny rules only the later policy carries.</param>
/// <param name="DenyRemoved">Deny rules only the earlier one carries.</param>
/// <param name="Widenings">
/// Added allow rules that generalise a rule the earlier policy already carried — the
/// <c>api.company.com</c> to <c>*.company.com</c> case, which looks like a one-word edit and is a change of scope.
/// </param>
/// <param name="NewlyPermitted">
/// Rules that permit something the earlier policy did not, whether by an allow being added or a deny being removed.
/// A rule already covered by one the earlier policy carried is **not** here: replacing <c>*.company.com</c> with
/// <c>api.company.com</c> adds a rule and permits nothing, and reporting it would be a warning about a policy being
/// tightened. Neither are the <see cref="Widenings"/>, so one edit produces one finding.
/// </param>
/// <param name="AllowedProtocolVersions">Accepted protocol revisions, added and removed.</param>
/// <param name="DenyDeprecatedCapabilities">Whether a deprecated capability is a violation, when it changed.</param>
public sealed record McpPolicyDiff(
    SurfaceValueChange? Default,
    IReadOnlyList<McpPolicyRule> AllowAdded,
    IReadOnlyList<McpPolicyRule> AllowRemoved,
    IReadOnlyList<McpPolicyRule> DenyAdded,
    IReadOnlyList<McpPolicyRule> DenyRemoved,
    IReadOnlyList<McpPolicyWidening> Widenings,
    IReadOnlyList<McpPolicyRule> NewlyPermitted,
    SurfaceSetDiff AllowedProtocolVersions,
    SurfaceValueChange? DenyDeprecatedCapabilities)
{
    /// <summary>Nothing about MCP changed.</summary>
    public static McpPolicyDiff Unchanged { get; } = new(
        null,
        [],
        [],
        [],
        [],
        [],
        [],
        SurfaceSetDiff.Unchanged,
        null);

    /// <summary>Gets a value indicating whether anything in the section changed.</summary>
    public bool HasChanges =>
        Default is not null
        || AllowAdded.Count > 0
        || AllowRemoved.Count > 0
        || DenyAdded.Count > 0
        || DenyRemoved.Count > 0
        || AllowedProtocolVersions.HasChanges
        || DenyDeprecatedCapabilities is not null;
}

/// <summary>
/// An allow rule that covers everything an existing rule covered, and more.
/// </summary>
/// <param name="Rule">The rule the later policy added.</param>
/// <param name="Generalises">The rule the earlier policy already carried, which the added one subsumes.</param>
public sealed record McpPolicyWidening(McpPolicyRule Rule, McpPolicyRule Generalises);
