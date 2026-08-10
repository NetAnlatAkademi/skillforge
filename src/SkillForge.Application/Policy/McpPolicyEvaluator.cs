using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Policy;

namespace SkillForge.Application.Policy;

/// <summary>
/// Judges the MCP servers a configuration declares against the organisation's policy.
/// </summary>
/// <remarks>
/// Pure: a policy and a configuration in, findings out. Nothing is connected to, and nothing is launched — this
/// asks what a file declares, which is the question a pull request can answer.
///
/// Two orderings are load-bearing. **Deny is checked before allow**, so a rule written to block something cannot be
/// undone by a broader allow beside it. And **an undeclared default blocks nothing**: it is reported once against
/// the policy (<c>SF8104</c>, an error) rather than turned into one finding per server. Guessing "deny" there would
/// bury the real problem — that nobody wrote the decision down — under a flood of findings the tool invented.
///
/// The severities here are not the <c>SF8001</c>–<c>SF8009</c> band's. Those describe what a declaration says and
/// are informational by design; these report a decision an organisation wrote down being broken, which is the one
/// thing SkillForge is willing to fail a build over.
/// </remarks>
public static class McpPolicyEvaluator
{
    /// <summary>Reports what the policy's own <c>mcp</c> section gets wrong, before any server is considered.</summary>
    /// <param name="mcp">The policy's MCP section.</param>
    /// <param name="policyPath">The policy file, so a finding points at the line somebody has to edit.</param>
    /// <returns><c>SF8104</c> when the section governs servers without denying by default; empty otherwise.</returns>
    public static IReadOnlyList<Diagnostic> DescribePolicy(PolicyMcp mcp, string policyPath)
    {
        ArgumentNullException.ThrowIfNull(mcp);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyPath);

        if (!mcp.GovernsServers || mcp.Default == McpPolicyDefault.Deny)
        {
            return [];
        }

        var reason = mcp.Default == McpPolicyDefault.Allow
            ? "the policy states 'default: allow', so a server no rule names is permitted"
            : "the policy states no default, so what happens to a server no rule names is not written down — "
                + "SkillForge did not guess, and checked only the rules that are there";

        return
        [
            Diagnostic.Error(
                DiagnosticCodes.McpPolicyFailOpen,
                $"The MCP policy does not deny by default: {reason}.",
                policyPath,
                suggestion: "Add 'default: deny' under 'mcp' and list what is permitted, or record a suppression "
                    + "with a reason if allowing by default is the decision.",
                fix: "mcp:\n  default: deny"),
        ];
    }

    /// <summary>Judges every server one configuration declares.</summary>
    /// <param name="mcp">The policy's MCP section.</param>
    /// <param name="configuration">What the configuration file declares.</param>
    /// <returns>Violations, in the standard report order; empty when every server is within policy.</returns>
    public static IReadOnlyList<Diagnostic> Evaluate(PolicyMcp mcp, McpConfigurationInspection configuration)
    {
        ArgumentNullException.ThrowIfNull(mcp);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!mcp.GovernsServers)
        {
            return [];
        }

        var findings = new List<Diagnostic>();

        foreach (var server in configuration.Servers)
        {
            if (Match(mcp.Deny, server) is { } denied)
            {
                findings.Add(Blocked(
                    server,
                    configuration.Path,
                    $"the policy denies it ({denied})"));

                continue;
            }

            var allowed = Match(mcp.Allow, server);
            if (allowed is null)
            {
                if (mcp.Default == McpPolicyDefault.Deny)
                {
                    findings.Add(Blocked(
                        server,
                        configuration.Path,
                        "no allow rule names it and the policy denies by default"));
                }

                continue;
            }

            if (allowed.Kind == McpPolicyRuleKind.ServerName)
            {
                findings.Add(Diagnostic.Warning(
                    DiagnosticCodes.McpPolicyMatchedByNameOnly,
                    $"'{server.Name}' is permitted only by its display name ({allowed}), which the configuration "
                        + "under review chooses for itself.",
                    configuration.Path,
                    suggestion: server.Transport == McpTransport.Http
                        ? "Name it by 'serverUrl' as well, so the rule is about an endpoint rather than a label."
                        : "Name it by 'serverCommand' as well, so the rule is about what would be launched."));
            }
        }

        return DiagnosticOrdering.Sort(findings);
    }

    /// <summary>
    /// Returns the first matching rule. First rather than best: a policy is read top to bottom by the person who
    /// wrote it, and a finding that quotes a rule they cannot find in order is a finding they will not trust.
    /// </summary>
    private static McpPolicyRule? Match(IReadOnlyList<McpPolicyRule> rules, McpServerDeclaration server) =>
        rules.FirstOrDefault(rule => McpPolicyRuleMatcher.Matches(rule, server));

    private static Diagnostic Blocked(McpServerDeclaration server, string path, string reason)
    {
        var target = server.Command is { Length: > 0 } command
            ? $" ({Describe(server.Transport)} {command})"
            : string.Empty;

        return Diagnostic.Error(
            DiagnosticCodes.McpServerBlockedByPolicy,
            $"'{server.Name}'{target} is not permitted: {reason}.",
            path,
            suggestion: "Remove the server, add it to the policy's allow list, or record a suppression with a "
                + "reason in the policy file.");
    }

    private static string Describe(McpTransport transport) => transport switch
    {
        McpTransport.Http => "url",
        McpTransport.Stdio => "command",
        _ => "declared as",
    };
}
