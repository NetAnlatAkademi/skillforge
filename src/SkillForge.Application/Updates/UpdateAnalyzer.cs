using SkillForge.Application.Provenance;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Updates;

namespace SkillForge.Application.Updates;

/// <summary>
/// Combines what an update changes with how it arrives.
/// </summary>
/// <remarks>
/// Pure, and that is the point: the rules below are the product's opinion about supply-chain risk, so they are
/// written where they can be read and argued with in one screen, with no file system underneath them.
///
/// The one idea holding all of it together: **the same change means different things depending on who chose to
/// take it.** A new shell script in a release somebody installed on purpose is ordinary. The same script arriving
/// overnight in a plugin that updates itself never passed a review, and if the publisher changed too, it did not
/// even come from the party the organisation decided to trust.
/// </remarks>
public static class UpdateAnalyzer
{
    /// <summary>Works out what an update does and how much it matters.</summary>
    /// <param name="basePath">The tree the update starts from.</param>
    /// <param name="targetPath">The tree it arrives at.</param>
    /// <param name="updateMode">How the target says it updates.</param>
    /// <param name="provenance">What changed about where the assets come from.</param>
    /// <param name="capabilities">What the target can reach that the base could not.</param>
    /// <returns>The analysis, with findings in the standard report order.</returns>
    public static UpdateAnalysis Analyze(
        string basePath,
        string targetPath,
        UpdateMode updateMode,
        ProvenanceDiff provenance,
        CapabilitySurfaceDiff capabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(capabilities);

        var unattended = updateMode is UpdateMode.Automatic or UpdateMode.Floating;
        var publisherChanged = provenance.Changed.Any(change => change.Publisher is { Before.Length: > 0 });

        var (risk, reason) = Classify(updateMode, unattended, publisherChanged, capabilities);

        var findings = new List<Diagnostic>(ProvenanceChangeDiagnostics.From(provenance));

        if (unattended && Expansions(capabilities) is { Count: > 0 } expansions)
        {
            findings.Add(Diagnostic.Error(
                DiagnosticCodes.DistributionUpdateAddedCapability,
                $"This update arrives without review ({updateMode}) and adds "
                    + $"{string.Join("; ", expansions)}.",
                targetPath,
                suggestion: "Pin the asset to a reviewed revision, or review what the update added before it "
                    + "reaches an agent."));
        }

        return new UpdateAnalysis(
            basePath,
            targetPath,
            updateMode,
            provenance,
            capabilities,
            risk,
            reason,
            DiagnosticOrdering.Sort(findings));
    }

    /// <summary>
    /// The most permissive update mode among a tree's assets.
    /// </summary>
    /// <remarks>
    /// One asset that updates itself makes the whole tree update itself, as far as a reviewer is concerned: the
    /// unreviewed change arrives whatever the other assets do. Taking the strictest mode instead would report the
    /// pinned majority and hide the one that matters.
    /// </remarks>
    /// <param name="report">The tree's provenance.</param>
    /// <returns>The mode to judge the update by.</returns>
    public static UpdateMode ModeOf(ProvenanceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Assets.Any(asset => asset.Source.UpdateMode == UpdateMode.Automatic))
        {
            return UpdateMode.Automatic;
        }

        if (report.Assets.Any(asset => asset.Source.UpdateMode == UpdateMode.Floating))
        {
            return UpdateMode.Floating;
        }

        return report.Assets.Any(asset => asset.Source.UpdateMode == UpdateMode.Pinned)
            ? UpdateMode.Pinned
            : UpdateMode.Unknown;
    }

    private static (UpdateRisk Risk, string Reason) Classify(
        UpdateMode updateMode,
        bool unattended,
        bool publisherChanged,
        CapabilitySurfaceDiff capabilities)
    {
        if (publisherChanged && unattended)
        {
            return (
                UpdateRisk.Critical,
                $"The publisher changed and the update arrives without review ({updateMode}).");
        }

        if (publisherChanged)
        {
            return (UpdateRisk.High, "The publisher changed, so future updates come from somebody else.");
        }

        if (unattended && Expansions(capabilities).Count > 0)
        {
            return (
                UpdateRisk.High,
                $"The update arrives without review ({updateMode}) and expands what the asset can reach.");
        }

        if (capabilities.Expands)
        {
            return (
                UpdateRisk.Medium,
                "The asset reaches further than it did. The update is pinned, so somebody chose to take it.");
        }

        return (
            UpdateRisk.Informational,
            capabilities.HasChanges
                ? "What changed does not let the asset reach any further."
                : "Nothing changed about what the asset can reach.");
    }

    /// <summary>
    /// Names the expansions that make an update more than a content change: something new that runs, reaches or
    /// reads. A new reference file is a change; it is not one of these.
    /// </summary>
    private static List<string> Expansions(CapabilitySurfaceDiff capabilities)
    {
        var expansions = new List<string>();

        Add(expansions, "MCP server", capabilities.McpServers.Added);
        Add(expansions, "hook", capabilities.Hooks.Added);
        Add(expansions, "script", capabilities.Scripts.Added);
        Add(expansions, "host", capabilities.Domains.Added);
        Add(expansions, "credential source", capabilities.CredentialSources.Added);

        // Shell and network arrive here when a skill's contents imply them without a new file: a SKILL.md that
        // started naming a URL reaches the network with nothing added to the directory.
        Add(
            expansions,
            "capability",
            [.. capabilities.Capabilities.Added.Where(capability =>
                capability is SkillCapabilities.ShellExecution or SkillCapabilities.NetworkAccess)]);

        return expansions;
    }

    private static void Add(List<string> expansions, string noun, IReadOnlyList<string> added)
    {
        if (added.Count == 0)
        {
            return;
        }

        var plural = added.Count == 1 ? noun : $"{noun}s";

        expansions.Add($"{added.Count} {plural} ({string.Join(", ", added)})");
    }
}
