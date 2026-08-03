using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Policy;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Policy;

/// <summary>
/// Judges one skill against an organisation's policy.
/// </summary>
/// <remarks>
/// Pure: a policy and a subject in, diagnostics out. Everything it reads was gathered before it ran.
///
/// Two properties are deliberate and load-bearing. A policy that is silent about something produces nothing —
/// there is no rule here with a default that forbids anything, so adopting SkillForge cannot start failing builds
/// over a decision nobody made. And every finding names the evidence: the script, the host, the tool, the line
/// count. A policy violation somebody cannot check is one they have to argue with rather than fix.
/// </remarks>
public static class PolicyEvaluator
{
    private const string EntryPoint = "SKILL.md";
    private const string PolicyFile = ".skillforge/policy.yaml";

    /// <summary>Applies a policy to a skill.</summary>
    /// <param name="policy">The organisation's policy.</param>
    /// <param name="subject">The skill and everything already known about it.</param>
    /// <returns>Violations, in the standard report order; empty when the skill is within policy.</returns>
    public static IReadOnlyList<Diagnostic> Evaluate(PolicyDocument policy, PolicySubject subject)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(subject);

        var findings = new List<Diagnostic>();

        AddShellFindings(policy, subject, findings);
        AddFilesystemWriteFindings(policy, subject, findings);
        AddDomainFindings(policy, subject, findings);
        AddProvenanceFindings(policy, subject, findings);
        AddSkillFindings(policy, subject, findings);

        return DiagnosticOrdering.Sort(
            findings.Where(finding => !policy.Suppresses(finding.Code, subject.Skill.Name)));
    }

    /// <summary>
    /// Names the rules that were read but could not be checked.
    /// </summary>
    /// <param name="policy">The organisation's policy.</param>
    /// <returns>One <c>SF9009</c> per unobservable rule; empty when every rule in the policy was evaluated.</returns>
    /// <remarks>
    /// A rule that never runs looks exactly like a rule that passed, and the difference is the whole value of
    /// having written the rule down. These are Info rather than warnings: nothing is wrong with the policy, it
    /// asks about something this command cannot see.
    /// </remarks>
    public static IReadOnlyList<Diagnostic> DescribeUnevaluatedRules(PolicyDocument policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var findings = new List<Diagnostic>();

        if (policy.Permissions.FilesystemWritePaths.Count > 0)
        {
            findings.Add(NotEvaluated(
                "permissions.filesystem.write.allowed lists paths",
                "A skill declares that it writes, never where. Whether it stays inside "
                    + $"{string.Join(", ", policy.Permissions.FilesystemWritePaths)} can only be answered by "
                    + "watching it run, which SkillForge does not do."));
        }

        if (policy.Provenance.RequirePackageHash)
        {
            findings.Add(NotEvaluated(
                "provenance.requirePackageHash",
                "Every package 'pack' produces carries a SHA-256, so this rule cannot fail here. Check it "
                    + "against a package's manifest instead."));
        }

        if (policy.Mcp is not null)
        {
            findings.Add(NotEvaluated(
                "the mcp section",
                "Protocol versions and deprecated capabilities are properties of a running server, not of a "
                    + "declaration. 'migrate inspect --probe-mcp' is what asks a server, and it reports SF8004 "
                    + "and SF8005."));
        }

        return findings;
    }

    /// <summary>
    /// Shell is judged on two independent signals: what the skill declares it runs, and what it ships. A skill
    /// that ships a script has shell reach whether or not it admitted to it.
    /// </summary>
    private static void AddShellFindings(
        PolicyDocument policy,
        PolicySubject subject,
        List<Diagnostic> findings)
    {
        if (policy.Permissions.ShellAllowed is not false)
        {
            return;
        }

        foreach (var command in subject.Configuration.ShellAllowed)
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.PolicyShellForbidden,
                $"The policy does not allow shell access, and the skill declares the command '{command}'.",
                SkillDefinition.ConfigurationFileName,
                suggestion: "Remove the command, or record a suppression with a reason in the policy file."));
        }

        foreach (var script in subject.Skill.Resources
            .Where(resource => resource.Kind == SkillResourceKind.Script))
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.PolicyShellForbidden,
                $"The policy does not allow shell access, and the skill ships the script '{script.RelativePath}'.",
                script.RelativePath,
                suggestion: "Remove the script, or record a suppression with a reason in the policy file."));
        }
    }

    /// <summary>
    /// Judged on the declaration alone. Nothing in a skill's contents proves it writes, and inferring a write
    /// from a script would fail every skill that ships one — the SF1006 measurement showed where that leads.
    /// </summary>
    private static void AddFilesystemWriteFindings(
        PolicyDocument policy,
        PolicySubject subject,
        List<Diagnostic> findings)
    {
        if (policy.Permissions.FilesystemWriteAllowed is not false)
        {
            return;
        }

        foreach (var tool in subject.Skill.Frontmatter.AllowedTools
            .Where(tool => tool.Contains("write", StringComparison.OrdinalIgnoreCase)
                && tool.Contains("filesystem", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.PolicyFilesystemWriteForbidden,
                $"The policy does not allow writing to the file system, and the skill declares '{tool}'.",
                EntryPoint,
                suggestion: "Remove the permission, or record a suppression with a reason in the policy file."));
        }
    }

    private static void AddDomainFindings(
        PolicyDocument policy,
        PolicySubject subject,
        List<Diagnostic> findings)
    {
        // Null is silence; an empty list is a decision that no host is allowed. Only one of them skips the check.
        if (policy.Permissions.AllowedDomains is not { } allowed)
        {
            return;
        }

        foreach (var host in Hosts(subject.Inspection.ExternalUrls))
        {
            if (allowed.Contains(host, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            findings.Add(Diagnostic.Error(
                DiagnosticCodes.PolicyDomainNotAllowed,
                $"The policy does not list '{host}' among the hosts a skill may point at.",
                EntryPoint,
                suggestion: "Remove the link, add the host to the policy, or record a suppression with a reason."));
        }
    }

    private static void AddProvenanceFindings(
        PolicyDocument policy,
        PolicySubject subject,
        List<Diagnostic> findings)
    {
        if (!policy.Provenance.RequireCommitSha || subject.Provenance.IdentifiesItsSource)
        {
            return;
        }

        // Naming which half is missing is the difference between a finding somebody can fix and one they have to
        // investigate. A dirty tree is the case a commit SHA alone would hide.
        var reason = subject.Provenance is { Commit: { Length: > 0 }, WorkingTreeIsDirty: true }
            ? "the skill has uncommitted changes, so the commit it names is not what would be published"
            : "the skill could not be traced to a repository, a commit and a path within it";

        findings.Add(Diagnostic.Error(
            DiagnosticCodes.PolicyProvenanceMissing,
            $"The policy requires a skill's origin to be identifiable, and {reason}.",
            EntryPoint,
            suggestion: "Commit the skill to a repository with a remote, or record a suppression with a reason."));
    }

    private static void AddSkillFindings(
        PolicyDocument policy,
        PolicySubject subject,
        List<Diagnostic> findings)
    {
        if (policy.Skills.RequireLicense && subject.Skill.Frontmatter.License is not { Length: > 0 })
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.PolicyLicenseMissing,
                "The policy requires a license and the skill declares none.",
                EntryPoint,
                suggestion: "Add a 'license' field to the frontmatter.",
                fix: "license: MIT"));
        }

        if (policy.Skills.MaxSkillFileLines is { } limit && subject.Skill.SkillFileLineCount > limit)
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.PolicySkillFileTooLong,
                $"The policy allows {limit} lines in {EntryPoint} and this skill has "
                    + $"{subject.Skill.SkillFileLineCount}.",
                EntryPoint,
                suggestion: "Move reference material into files under the skill and link to them."));
        }
    }

    /// <summary>
    /// Hosts rather than URLs, matching what <c>diff</c> compares: an allow-list is a list of who a skill may
    /// talk to, not of which pages it may link.
    /// </summary>
    private static IEnumerable<string> Hosts(IEnumerable<string> urls) =>
        urls
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Host : null)
            .Where(host => host is { Length: > 0 })
            .Select(host => host!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

    private static Diagnostic NotEvaluated(string rule, string reason) =>
        Diagnostic.Info(
            DiagnosticCodes.PolicyRuleNotEvaluated,
            $"{rule} was read but not checked: {reason}",
            PolicyFile,
            suggestion: "Remove the rule, or keep it and know that this command does not enforce it.");
}
