using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Provenance;

/// <summary>
/// Turns the part of a provenance diff that is a finding into diagnostics.
/// </summary>
/// <remarks>
/// Drift only, and drift in one direction. An asset that gained a publisher, a marketplace or a pin is shown in
/// the report and coded nowhere: a reviewer who tightened a distribution does not need a warning about it, and a
/// command that warned about every edit teaches people to skip its output.
///
/// The asymmetry is deliberate everywhere it appears. <c>company</c> becoming <c>unknown-publisher</c> is a
/// finding; <c>null</c> becoming <c>company</c> is not. Losing an attribution is the event worth stopping for.
/// </remarks>
public static class ProvenanceChangeDiagnostics
{
    /// <summary>Reports what drifted between two trees.</summary>
    /// <param name="diff">The compared reports.</param>
    /// <returns>Findings, in the standard report order; empty when nothing drifted.</returns>
    public static IReadOnlyList<Diagnostic> From(ProvenanceDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var findings = new List<Diagnostic>();

        foreach (var change in diff.Changed)
        {
            AddChangeFindings(change, findings);
        }

        foreach (var asset in diff.Added)
        {
            AddArrivalFindings(asset, findings);
        }

        return DiagnosticOrdering.Sort(findings);
    }

    private static void AddChangeFindings(AssetProvenanceChange change, List<Diagnostic> findings)
    {
        // An asset that inherits its distribution reports none of it. The plugin it came from is in the same diff
        // and reports the change once; repeating it per skill would turn one edit into twenty findings.
        if (change.After.DistributedBy is null)
        {
            AddPublisherFinding(change, findings);
            AddMarketplaceFinding(change, findings);
            AddUpdateModeFindings(change, findings);
        }

        AddContentFindings(change, findings);
    }

    /// <summary>
    /// A publisher that changed is an error, and the only one this command raises about a single field.
    /// </summary>
    /// <remarks>
    /// Whoever the organisation decided to trust is not who is shipping this now. Everything else here describes
    /// how an asset arrives; this describes who it arrives from.
    /// </remarks>
    private static void AddPublisherFinding(AssetProvenanceChange change, List<Diagnostic> findings)
    {
        if (change.Publisher is not { Before: { Length: > 0 } before } publisher)
        {
            return;
        }

        findings.Add(Diagnostic.Error(
            DiagnosticCodes.DistributionPublisherChanged,
            $"The publisher of '{change.Name}' changed from '{before}' to '{Word(publisher.After)}'.",
            change.Path,
            suggestion: "Confirm the new publisher is the one the organisation intended to take this from. A "
                + "publisher change carries every future update with it."));
    }

    private static void AddMarketplaceFinding(AssetProvenanceChange change, List<Diagnostic> findings)
    {
        if (change.Marketplace is not { Before: { Length: > 0 } before } marketplace)
        {
            return;
        }

        findings.Add(Diagnostic.Warning(
            DiagnosticCodes.DistributionMarketplaceChanged,
            $"'{change.Name}' is now distributed through '{Word(marketplace.After)}' rather than '{before}'.",
            change.Path,
            suggestion: "Check that the new marketplace is one the organisation permits."));
    }

    private static void AddUpdateModeFindings(AssetProvenanceChange change, List<Diagnostic> findings)
    {
        if (change.UpdateMode is null)
        {
            return;
        }

        var before = change.Before.Source.UpdateMode;
        var after = change.After.Source.UpdateMode;

        if (after == UpdateMode.Automatic && before != UpdateMode.Automatic)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.DistributionAutomaticUpdate,
                $"'{change.Name}' now updates itself ({before} -> {after}), so what arrives next will not pass "
                    + "through whoever reviews this change.",
                change.Path,
                suggestion: "Pin the asset to a revision, or record the decision to let it update unattended. "
                    + "'update analyze' shows what a given update would add."));
        }

        // A pin that became a branch is the same event as a pin that became automatic, one step earlier: the
        // revision is no longer decided by anyone reviewing this.
        if (after == UpdateMode.Floating && before == UpdateMode.Pinned)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.DistributionVersionFloating,
                $"'{change.Name}' no longer names an immutable revision: {Word(change.UpdateMode.Before)} became "
                    + $"{Word(change.UpdateMode.After)}.",
                change.Path,
                suggestion: "Name a commit or a version tag, or record why a moving revision is intended."));
        }
    }

    private static void AddContentFindings(AssetProvenanceChange change, List<Diagnostic> findings)
    {
        // The fingerprint moved while the version stood still, so anyone pinned to that version received
        // different bytes and was told nothing. A version that changed too is an ordinary release.
        if (change.Fingerprint is not null && change.Version is null && change.After.Source.Version is { Length: > 0 })
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.DistributionHashChanged,
                $"The contents of '{change.Name}' changed while its declared version stayed at "
                    + $"'{change.After.Source.Version}'.",
                change.Path,
                suggestion: "Publish the change under a new version, or explain why the same version now holds "
                    + "different bytes."));
        }

        if (change.After.Status == ProvenanceStatus.Unknown && change.Before.Status != ProvenanceStatus.Unknown)
        {
            findings.Add(Unknown(change.After));
        }

        if (change.Upstream is { Before: null, After: { Length: > 0 } })
        {
            findings.Add(Forked(change.After));
        }

        if (change.After.LocallyModifiedFiles > 0 && change.Before.LocallyModifiedFiles == 0)
        {
            findings.Add(LocallyModified(change.After));
        }
    }

    /// <summary>
    /// An asset that was not there before is judged on its own state, because there is nothing to compare it to
    /// and "it arrived already untraceable" is exactly what a reviewer needs to see.
    /// </summary>
    private static void AddArrivalFindings(AssetProvenance asset, List<Diagnostic> findings)
    {
        if (asset.Status == ProvenanceStatus.Unknown)
        {
            findings.Add(Unknown(asset));
        }

        if (asset.DeclaredUpstream is { Length: > 0 })
        {
            findings.Add(Forked(asset));
        }

        if (asset.LocallyModifiedFiles > 0)
        {
            findings.Add(LocallyModified(asset));
        }

        if (asset.Source.UpdateMode == UpdateMode.Automatic && asset.DistributedBy is null)
        {
            findings.Add(Diagnostic.Warning(
                DiagnosticCodes.DistributionAutomaticUpdate,
                $"'{asset.Name}' arrived set to update itself, so what it becomes next will not pass through a "
                    + "review.",
                asset.Path,
                suggestion: "Pin the asset to a revision, or record the decision to let it update unattended."));
        }
    }

    private static Diagnostic Unknown(AssetProvenance asset) => Diagnostic.Warning(
        DiagnosticCodes.ProvenanceUnknown,
        $"Nothing says where '{asset.Name}' came from: no repository, no marketplace and no manifest names a "
            + "source.",
        asset.Path,
        suggestion: "Add it through a marketplace or a repository, or record where it came from. SkillForge "
            + "reports the gap rather than guessing at an origin.");

    private static Diagnostic Forked(AssetProvenance asset) => Diagnostic.Info(
        DiagnosticCodes.ProvenanceForked,
        $"'{asset.Name}' declares an upstream it is not distributed from: {asset.DeclaredUpstream}.",
        asset.Path,
        suggestion: "Nothing is wrong with a fork. Confirm it is one somebody intended, and that upstream fixes "
            + "still reach it.");

    private static Diagnostic LocallyModified(AssetProvenance asset) => Diagnostic.Warning(
        DiagnosticCodes.ProvenanceLocallyModified,
        $"'{asset.Name}' has {asset.LocallyModifiedFiles} uncommitted file(s), so the commit in its provenance "
            + "does not describe what is on disk.",
        asset.Path,
        suggestion: "Commit the change, or read the provenance as naming a revision this copy is no longer at.");

    private static string Word(string? value) => value is { Length: > 0 } ? value : "unknown";
}
