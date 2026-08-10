using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Policy;

/// <summary>
/// Turns the part of a policy diff that is a finding into diagnostics.
/// </summary>
/// <remarks>
/// Only relaxations. A policy that got stricter is shown in the report and reported nowhere else, because a
/// reviewer who tightened a rule does not need a warning about it — and a command that warned about every edit
/// would teach people to skip its output, which is how the rule that mattered gets skipped too.
///
/// **A change is only coded when some command enforces the rule it changes.** A wider
/// <c>filesystem.write.allowed</c> path list, a changed <c>requirePackageHash</c>, a changed
/// <c>allowedProtocolVersions</c>: <c>policy check</c> reports all three as <c>SF9009</c>, unobservable. Coding a
/// change to a rule that never runs would be a warning about a guarantee nobody has. They appear in the report
/// instead, which is where a fact without a verdict belongs.
/// </remarks>
public static class PolicyChangeDiagnostics
{
    /// <summary>Reports what the later policy permits that the earlier one did not.</summary>
    /// <param name="diff">The compared policies.</param>
    /// <returns>Findings, in the standard report order; empty when nothing was relaxed.</returns>
    public static IReadOnlyList<Diagnostic> From(PolicyDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var findings = new List<Diagnostic>();

        AddMcpFindings(diff, findings);
        AddPermissionFindings(diff, findings);
        AddRequirementFindings(diff, findings);

        return DiagnosticOrdering.Sort(findings);
    }

    private static void AddMcpFindings(PolicyDiff diff, List<Diagnostic> findings)
    {
        var mcp = diff.Mcp;

        if (mcp.Default is { } change && Weakened(change))
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.McpPolicyFailOpen,
                $"The MCP policy stopped denying by default: '{Word(change.Before)}' became "
                    + $"'{Word(change.After)}', so a server no rule names is no longer refused.",
                diff.AfterPath,
                suggestion: "Restore 'default: deny' under 'mcp', or record why allowing by default is the "
                    + "decision.",
                fix: "mcp:\n  default: deny"));
        }

        foreach (var widening in mcp.Widenings)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.McpPolicyWildcardExpanded,
                $"The MCP allow list widened: '{widening.Generalises.Pattern}' is now covered by "
                    + $"'{widening.Rule.Pattern}', which also matches everything else it fits.",
                diff.AfterPath,
                suggestion: "List the endpoints the organisation actually uses, or record why the wider pattern "
                    + "is intended."));
        }

        foreach (var rule in mcp.NewlyPermitted)
        {
            var viaDeny = mcp.DenyRemoved.Contains(rule);
            var how = viaDeny ? "a deny rule was removed" : "an allow rule was added";

            findings.Add(rule.Kind switch
            {
                McpPolicyRuleKind.ServerUrl => Diagnostic.Warning(
                    DiagnosticCodes.McpPolicyRemoteDomainPermitted,
                    $"A remote MCP endpoint is permitted that was not before: {rule.Pattern} ({how}).",
                    diff.AfterPath,
                    suggestion: "Confirm the endpoint is one the organisation operates or has reviewed."),

                McpPolicyRuleKind.ServerCommand => Diagnostic.Warning(
                    DiagnosticCodes.McpPolicyLocalCommandPermitted,
                    $"A local MCP command is permitted that was not before: {Command(rule)} ({how}).",
                    diff.AfterPath,
                    suggestion: "A local command runs on the developer's machine with their privileges. Confirm "
                        + "the package it resolves is pinned and reviewed."),

                _ => Diagnostic.Warning(
                    DiagnosticCodes.McpPolicyMatchedByNameOnly,
                    $"A server is permitted by display name alone: {rule.Pattern} ({how}).",
                    diff.AfterPath,
                    suggestion: "A name is chosen by the configuration being reviewed. Name the endpoint or the "
                        + "command as well."),
            });
        }
    }

    private static void AddPermissionFindings(PolicyDiff diff, List<Diagnostic> findings)
    {
        if (diff.ShellAllowed is { } shell && Weakened(shell, stricter: "forbidden"))
        {
            findings.Add(Relaxed(diff, "permissions.shell.allowed", shell));
        }

        if (diff.FilesystemWriteAllowed is { } write && Weakened(write, stricter: "forbidden"))
        {
            findings.Add(Relaxed(diff, "permissions.filesystem.write.allowed", write));
        }

        // A list that disappears takes the whole host check with it, which no single added entry does.
        if (diff.AllowedDomainsDeclared is { After: "not stated" })
        {
            findings.Add(Relaxed(
                diff,
                "permissions.network.allowedDomains",
                new SurfaceValueChange("declared", "not stated")));
        }
        else
        {
            foreach (var host in diff.AllowedDomains.Added)
            {
                findings.Add(Diagnostic.Warning(
                    DiagnosticCodes.PolicyRelaxed,
                    $"permissions.network.allowedDomains gained '{host}', so skills may now point at it.",
                    diff.AfterPath,
                    suggestion: "Confirm the host is one the organisation has reviewed."));
            }
        }
    }

    private static void AddRequirementFindings(PolicyDiff diff, List<Diagnostic> findings)
    {
        if (diff.RequireCommitSha is { } provenance && Weakened(provenance, stricter: "required"))
        {
            findings.Add(Relaxed(diff, "provenance.requireCommitSha", provenance));
        }

        if (diff.RequireLicense is { } license && Weakened(license, stricter: "required"))
        {
            findings.Add(Relaxed(diff, "skills.requireLicense", license));
        }

        if (diff.MaxSkillFileLines is { } lines && LimitRaised(lines))
        {
            findings.Add(Relaxed(diff, "skills.maxSkillFileLines", lines));
        }

        foreach (var suppression in diff.Suppressions.Added)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.PolicyRelaxed,
                $"A rule is now silenced that was not before: {suppression}.",
                diff.AfterPath,
                suggestion: "The reason recorded beside it in the policy file is what makes this reviewable."));
        }
    }

    private static Diagnostic Relaxed(PolicyDiff diff, string rule, SurfaceValueChange change) =>
        Diagnostic.Warning(
            DiagnosticCodes.PolicyRelaxed,
            $"{rule} was relaxed: '{Word(change.Before)}' became '{Word(change.After)}'.",
            diff.AfterPath,
            suggestion: "Confirm the change is intended, or restore the rule.");

    /// <summary>
    /// A change away from the stricter of two words. "Not stated" counts as relaxed, because a rule that stops
    /// being written down stops being applied.
    /// </summary>
    private static bool Weakened(SurfaceValueChange change, string stricter) =>
        string.Equals(change.Before, stricter, StringComparison.Ordinal);

    private static bool Weakened(SurfaceValueChange change) =>
        string.Equals(change.Before, nameof(McpPolicyDefault.Deny), StringComparison.Ordinal);

    /// <summary>
    /// A limit that grew, or stopped existing, permits a skill the earlier policy refused. A limit that appears
    /// where there was none does the opposite, which is why the unstated case is not treated as zero.
    /// </summary>
    private static bool LimitRaised(SurfaceValueChange change)
    {
        if (change.After is "not stated")
        {
            return change.Before is not "not stated";
        }

        return int.TryParse(change.Before, out var before)
            && int.TryParse(change.After, out var after)
            && after > before;
    }

    private static string Command(McpPolicyRule rule) =>
        rule.Arguments.Count > 0 ? $"{rule.Pattern} {string.Join(' ', rule.Arguments)}" : rule.Pattern;

    private static string Word(string? value) => value ?? "not stated";
}
