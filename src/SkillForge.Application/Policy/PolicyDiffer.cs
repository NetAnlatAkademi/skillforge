using System.Globalization;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Policy;

/// <summary>
/// Compares two policy files by what they decide.
/// </summary>
/// <remarks>
/// Pure, and the same shape as the two differs beside it: two documents in, one diff out, no judgement. Which parts
/// of the result are findings is <see cref="PolicyChangeDiagnostics"/>'s decision.
///
/// An absent <c>mcp</c> section is compared as an empty one rather than skipped, so a section being deleted reads
/// as every rule in it being removed — which is exactly what it is, and exactly what a reviewer needs to see.
/// </remarks>
public static class PolicyDiffer
{
    private static readonly PolicyMcp NoMcp = new([], false, McpPolicyDefault.NotDeclared, [], []);

    /// <summary>Compares two policies.</summary>
    /// <param name="beforePath">Path of the earlier policy, for the report.</param>
    /// <param name="before">The earlier policy.</param>
    /// <param name="afterPath">Path of the later policy.</param>
    /// <param name="after">The later policy.</param>
    /// <returns>What the two decide differently.</returns>
    public static PolicyDiff Compare(
        string beforePath,
        PolicyDocument before,
        string afterPath,
        PolicyDocument after)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(beforePath);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterPath);
        ArgumentNullException.ThrowIfNull(after);

        return new PolicyDiff(
            beforePath,
            afterPath,
            SurfaceValueChange.Between(
                Permission(before.Permissions.ShellAllowed),
                Permission(after.Permissions.ShellAllowed)),
            SurfaceValueChange.Between(
                Permission(before.Permissions.FilesystemWriteAllowed),
                Permission(after.Permissions.FilesystemWriteAllowed)),
            SurfaceSetDiff.Between(
                before.Permissions.FilesystemWritePaths,
                after.Permissions.FilesystemWritePaths),
            SurfaceValueChange.Between(
                Declared(before.Permissions.AllowedDomains),
                Declared(after.Permissions.AllowedDomains)),
            SurfaceSetDiff.Between(
                before.Permissions.AllowedDomains ?? [],
                after.Permissions.AllowedDomains ?? []),
            SurfaceValueChange.Between(
                Required(before.Provenance.RequireCommitSha),
                Required(after.Provenance.RequireCommitSha)),
            SurfaceValueChange.Between(
                Required(before.Provenance.RequirePackageHash),
                Required(after.Provenance.RequirePackageHash)),
            SurfaceValueChange.Between(
                Required(before.Skills.RequireLicense),
                Required(after.Skills.RequireLicense)),
            SurfaceValueChange.Between(
                Lines(before.Skills.MaxSkillFileLines),
                Lines(after.Skills.MaxSkillFileLines)),
            SurfaceSetDiff.Between(
                before.Suppressions.Select(Describe),
                after.Suppressions.Select(Describe)),
            CompareMcp(before.Mcp ?? NoMcp, after.Mcp ?? NoMcp));
    }

    private static McpPolicyDiff CompareMcp(PolicyMcp before, PolicyMcp after)
    {
        var allowAdded = Except(after.Allow, before.Allow);
        var denyRemoved = Except(before.Deny, after.Deny);

        // Only added allow rules are tested for widening, and only against allow rules that were already there. A
        // removed deny is a permission by any reading, but calling it a "widening" of an allow rule it has no
        // relationship to would put a made-up pairing in front of a reviewer.
        var widenings = allowAdded
            .Select(rule => new
            {
                Rule = rule,
                Generalises = before.Allow.FirstOrDefault(existing =>
                    McpPolicyRuleMatcher.Generalises(rule, existing)),
            })
            .Where(pair => pair.Generalises is not null)
            .Select(pair => new McpPolicyWidening(pair.Rule, pair.Generalises!))
            .ToArray();

        var widened = widenings.Select(widening => widening.Rule).ToHashSet();

        // Two edits add a rule and permit nothing new: narrowing an allow that a broader one still covers, and
        // deleting a deny that a broader deny still covers. Both are policies being tightened, and warning about
        // them is how a command earns the reputation that makes its real findings ignored.
        var newlyPermitted = new List<McpPolicyRule>(
            allowAdded.Where(rule =>
                !widened.Contains(rule)
                && !before.Allow.Any(existing => McpPolicyRuleMatcher.Generalises(existing, rule))));

        newlyPermitted.AddRange(denyRemoved.Where(rule =>
            !after.Deny.Any(remaining => McpPolicyRuleMatcher.Generalises(remaining, rule))));

        return new McpPolicyDiff(
            SurfaceValueChange.Between(before.Default.ToString(), after.Default.ToString()),
            allowAdded,
            Except(before.Allow, after.Allow),
            Except(after.Deny, before.Deny),
            denyRemoved,
            widenings,
            newlyPermitted,
            SurfaceSetDiff.Between(before.AllowedProtocolVersions, after.AllowedProtocolVersions),
            SurfaceValueChange.Between(
                Required(before.DenyDeprecatedCapabilities),
                Required(after.DenyDeprecatedCapabilities)));
    }

    /// <summary>
    /// Set difference that keeps the order the policy file wrote, so a report reads down the file rather than
    /// alphabetically through it.
    /// </summary>
    private static IReadOnlyList<McpPolicyRule> Except(
        IReadOnlyList<McpPolicyRule> rules,
        IReadOnlyList<McpPolicyRule> others)
    {
        var known = others.ToHashSet();

        return [.. rules.Where(rule => !known.Contains(rule))];
    }

    private static string Permission(bool? allowed) => allowed switch
    {
        true => "allowed",
        false => "forbidden",
        _ => "not stated",
    };

    private static string Required(bool required) => required ? "required" : "not required";

    private static string Declared(IReadOnlyList<string>? values) => values is null ? "not stated" : "declared";

    private static string Lines(int? limit) =>
        limit?.ToString(CultureInfo.InvariantCulture) ?? "not stated";

    private static string Describe(PolicySuppression suppression) =>
        suppression.Skill is { Length: > 0 } skill
            ? $"{suppression.Code} ({skill})"
            : suppression.Code;
}
