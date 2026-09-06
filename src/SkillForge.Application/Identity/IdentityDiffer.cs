using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Identity;

namespace SkillForge.Application.Identity;

/// <summary>
/// Compares the identities two configurations use, and reports the changes that widen what an agent can do.
/// </summary>
/// <remarks>
/// Pure, like every other differ here. Servers are matched by name, which is the only identity a configuration
/// gives them; a renamed server is one removed and one added, and saying so is more honest than guessing that two
/// differently named entries are the same server.
///
/// Widenings only. An identity that became narrower — a workload identity replaced by a person's session, a scope
/// dropped — is shown and coded nowhere.
/// </remarks>
public static class IdentityDiffer
{
    /// <summary>Compares two sets of server identities.</summary>
    /// <param name="before">The earlier report.</param>
    /// <param name="after">The later report.</param>
    /// <returns>The difference, every list ordered by server name.</returns>
    public static IdentityDiff Compare(McpIdentityReport before, McpIdentityReport after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changed = new List<ServerIdentityChange>();
        var added = new List<McpServerIdentity>();

        foreach (var server in after.Servers)
        {
            var match = before.Servers.FirstOrDefault(candidate =>
                string.Equals(candidate.ServerName, server.ServerName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                added.Add(server);
                continue;
            }

            var change = Compare(match, server);
            if (change.HasChanges)
            {
                changed.Add(change);
            }
        }

        var removed = before.Servers.Where(server => !after.Servers.Any(candidate =>
            string.Equals(candidate.ServerName, server.ServerName, StringComparison.OrdinalIgnoreCase)));

        return new IdentityDiff(
            before.Path,
            after.Path,
            [.. changed.OrderBy(change => change.ServerName, StringComparer.Ordinal)],
            [.. added.OrderBy(server => server.ServerName, StringComparer.Ordinal)],
            [.. removed.OrderBy(server => server.ServerName, StringComparer.Ordinal)]);
    }

    /// <summary>Reports the identity changes that widen what an agent can do.</summary>
    /// <param name="diff">The compared reports.</param>
    /// <returns>Findings, in the standard report order; empty when nothing widened.</returns>
    public static IReadOnlyList<Diagnostic> Findings(IdentityDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var findings = new List<Diagnostic>();

        foreach (var change in diff.Changed)
        {
            AddFindings(change, findings);
        }

        return DiagnosticOrdering.Sort(findings);
    }

    private static void AddFindings(ServerIdentityChange change, List<Diagnostic> findings)
    {
        if (change.Type is { } type)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.IdentityTypeChanged,
                $"'{change.ServerName}' is now reached with a different kind of identity: {type.Before} became "
                    + $"{type.After}.",
                change.After.SourcePath,
                suggestion: "Confirm the new identity is the one intended, and that what it is allowed to do is "
                    + "no wider than what the old one was."));
        }

        if (change.Delegation is { After: "true" })
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.DelegationEnabled,
                $"'{change.ServerName}' now acts on behalf of another party.",
                change.After.SourcePath,
                suggestion: "Check who it acts for, and that the delegation is bounded to what the task needs."));
        }

        if (change.Lifetime is { After: "long-lived" })
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.IdentityBecameLongLived,
                $"'{change.ServerName}' now uses a credential that does not expire on its own.",
                change.After.SourcePath,
                suggestion: "Prefer a short-lived credential, or record how this one is rotated and revoked."));
        }

        if (change.Scopes.Added.Count > 0)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.DelegationScopeExpanded,
                $"'{change.ServerName}' asks for scopes it did not before: "
                    + $"{string.Join(", ", change.Scopes.Added)}.",
                change.After.SourcePath,
                suggestion: "Grant the narrowest scope the task needs, or record why the wider one is required."));
        }
    }

    private static ServerIdentityChange Compare(McpServerIdentity before, McpServerIdentity after) => new(
        after.ServerName,
        before,
        after,
        SurfaceValueChange.Between(before.Identity.Type.ToString(), after.Identity.Type.ToString()),
        SurfaceValueChange.Between(before.Identity.Issuer, after.Identity.Issuer),
        SurfaceValueChange.Between(Word(before.Identity.IsDelegated), Word(after.Identity.IsDelegated)),
        SurfaceValueChange.Between(Lifetime(before), Lifetime(after)),
        SurfaceSetDiff.Between(before.Identity.Scopes, after.Identity.Scopes));

    private static string Word(bool value) => value ? "true" : "false";

    private static string Lifetime(McpServerIdentity server) =>
        server.Identity.IsLongLived ? "long-lived" : "short-lived";
}
