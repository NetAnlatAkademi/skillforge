using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;

namespace SkillForge.Application.Diffing;

/// <summary>
/// Restates a surface diff as diagnostics, so a diff can be reported in the formats findings are reported in.
/// </summary>
/// <remarks>
/// A diff is not a list of findings, and most of it never becomes one: a changed description, a new reference
/// file, a compatibility declaration. Those are shown in the console and JSON reports and stop there.
///
/// What does become a finding is the part a reviewer would want blocked or annotated — the three ways a skill's
/// reach grows, the version that stayed silent while it grew, and the validation findings the later revision
/// introduced. Anything else would be using SARIF to carry a summary rather than a finding.
/// </remarks>
public static class SurfaceChangeDiagnostics
{
    private const string EntryPoint = "SKILL.md";

    /// <summary>Restates the parts of a diff that are findings.</summary>
    /// <param name="diff">The diff to restate.</param>
    /// <returns>Diagnostics in the standard report order; empty when nothing about the diff is a finding.</returns>
    public static IReadOnlyList<Diagnostic> From(SkillSurfaceDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var diagnostics = new List<Diagnostic>();

        foreach (var permission in diff.DeclaredTools.Added)
        {
            diagnostics.Add(Diagnostic.Warning(
                DiagnosticCodes.PermissionAdded,
                $"The skill now declares the permission '{permission}', which the earlier revision did not.",
                EntryPoint,
                suggestion: "Confirm the skill needs it, and that whoever consumes the skill expects it."));
        }

        foreach (var script in diff.Scripts.Added)
        {
            diagnostics.Add(Diagnostic.Warning(
                DiagnosticCodes.ScriptAdded,
                $"The skill now ships the script '{script}', which the earlier revision did not.",
                script,
                suggestion: "Read what it does before the change is merged."));
        }

        foreach (var domain in diff.ExternalDomains.Added)
        {
            diagnostics.Add(Diagnostic.Warning(
                DiagnosticCodes.ExternalDomainAdded,
                $"The skill now points at '{domain}', which the earlier revision did not.",
                EntryPoint,
                suggestion: "Confirm the host is one this repository is willing to talk to."));
        }

        // One finding for everything given up, rather than one each: a narrowing is not a risk, and a skill that
        // dropped six permissions would otherwise bury the additions it made in the same change.
        var narrowed = diff.DeclaredTools.Removed
            .Concat(diff.Scripts.Removed)
            .Concat(diff.ExternalDomains.Removed)
            .ToArray();

        if (narrowed.Length > 0)
        {
            diagnostics.Add(Diagnostic.Info(
                DiagnosticCodes.ReachNarrowed,
                $"The skill no longer reaches: {string.Join(", ", narrowed)}.",
                EntryPoint,
                suggestion: "Check that nothing depending on the skill relied on what was removed."));
        }

        if (diff.VersionIsSilentAboutGrowth)
        {
            diagnostics.Add(Diagnostic.Warning(
                DiagnosticCodes.VersionSilentAboutGrowth,
                "The skill's reach grew while its declared version stayed the same, so anyone pinned to that "
                    + "version received the change without being told.",
                EntryPoint,
                suggestion: "Raise the version alongside the change."));
        }

        diagnostics.AddRange(diff.NewFindings);

        return DiagnosticOrdering.Sort(diagnostics);
    }
}
