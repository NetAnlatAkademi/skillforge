using SkillForge.Application.Mcp;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Discovery;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Discovery;

/// <summary>
/// Asks each discovered resource whether it is what its listing says it is.
/// </summary>
/// <remarks>
/// The whole point of the feature, and the sentence that justifies it: **a registry can say a server has twelve
/// tools while the server answers with seventeen.** Discovery metadata is written by a publisher; a tool surface
/// is answered by a process. Comparing them is the one question a registry cannot answer about itself.
///
/// **It reuses <see cref="McpProber"/> rather than probing anything itself.** That is not tidiness: a second
/// probing stack would have its own idea of what a tool list is, and the two would eventually disagree about a
/// server — at which point the useful output of this command becomes an argument between two parts of the same
/// tool. Reusing the prober also means it inherits, for free, the properties that were argued out there: HTTP
/// only, a stdio server never launched, and <c>tools/list</c> read to the end under its three bounds.
///
/// **Describing, not judging.** This produces drift findings — differences, each with both sides and where they
/// were read. Whether a difference is acceptable is <c>policy check</c>'s decision. Keeping the two apart is what
/// lets "unexpected runtime tool: delete_database" be a description and "delete_database is not permitted" be a
/// violation, rather than one confused sentence that is neither.
/// </remarks>
public sealed class DiscoveryVerifier
{
    /// <summary>
    /// The provider id a synthesised declaration carries. Not a real provider: it records that the declaration
    /// came from a registry rather than from a configuration file on this machine, so nothing downstream mistakes
    /// a discovered endpoint for an installed one.
    /// </summary>
    public const string DiscoveryProviderId = "discovery";

    private readonly McpProber _prober;

    /// <summary>Initialises the verifier.</summary>
    /// <param name="prober">
    /// The existing MCP prober. Everything about how a server is asked — the revisions, the fallback, the
    /// pagination, the refusal to launch a local command — belongs to it.
    /// </param>
    public DiscoveryVerifier(McpProber prober)
    {
        ArgumentNullException.ThrowIfNull(prober);
        _prober = prober;
    }

    /// <summary>Verifies what can be verified out of a discovery result.</summary>
    /// <param name="result">What the registry listed.</param>
    /// <param name="query">What was searched for, carried into the report.</param>
    /// <param name="probe">
    /// Whether to ask the servers about themselves. <see langword="false"/> reports every resource as
    /// <see cref="DiscoveryVerificationStatus.NotProbed"/> and makes no request, which is what makes the network
    /// step a separate decision from the search.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The report, resources ordered by id and findings in the standard order.</returns>
    public async Task<DiscoveryVerificationReport> VerifyAsync(
        RemoteDiscoveryResult result,
        string query,
        bool probe,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var verifications = new List<ResourceVerification>(result.Resources.Count);
        var findings = new List<Diagnostic>(result.Diagnostics);

        foreach (var resource in result.Resources.OrderBy(entry => entry.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var verification = await VerifyOneAsync(resource, probe, cancellationToken).ConfigureAwait(false);

            verifications.Add(verification);
            findings.AddRange(Findings(verification));
        }

        return new DiscoveryVerificationReport(
            result.Registry.ToString(),
            query,
            verifications,
            DiagnosticOrdering.Sort(findings));
    }

    private async Task<ResourceVerification> VerifyOneAsync(
        DiscoveredResource resource,
        bool probe,
        CancellationToken cancellationToken)
    {
        var evidence = new DiscoveryEvidence(resource.SourceRegistry, resource.Endpoint?.ToString(), null);

        if (!resource.IsRemotelyVerifiable)
        {
            return new ResourceVerification(
                resource,
                DiscoveryVerificationStatus.UnsupportedResource,
                Unsupported(resource),
                [],
                null,
                []);
        }

        if (!probe)
        {
            return new ResourceVerification(
                resource,
                DiscoveryVerificationStatus.NotProbed,
                "not asked — verification makes a network request, so it is opt-in",
                [],
                null,
                []);
        }

        // One synthesised declaration, so the existing prober does the asking. The registry URL is its source
        // path, which is where the declared half of every finding was read.
        var declaration = new McpServerDeclaration(
            resource.Name,
            DiscoveryProviderId,
            McpTransport.Http,
            resource.Endpoint!.ToString(),
            [],
            [],
            resource.SourceRegistry,
            []);

        var outcome = await _prober
            .ProbeAsync([declaration], cancellationToken)
            .ConfigureAwait(false);

        var serverProbe = outcome.Probes.Count > 0 ? outcome.Probes[0] : null;

        if (serverProbe is not { Status: McpProbeStatus.Answered })
        {
            return new ResourceVerification(
                resource,
                DiscoveryVerificationStatus.ProbeFailed,
                serverProbe?.Detail ?? "the server did not answer",
                [],
                null,
                []);
        }

        var drifts = Compare(
            resource,
            serverProbe,
            evidence with { ProbedRevision = serverProbe.AnsweredRevision });

        return new ResourceVerification(
            resource,
            drifts.Count == 0
                ? DiscoveryVerificationStatus.VerifiedNoDrift
                : DiscoveryVerificationStatus.DriftDetected,
            null,
            serverProbe.ToolsOrEmpty,
            serverProbe.Paging,
            drifts);
    }

    /// <summary>
    /// Compares the declared side with the runtime side, where they are comparable at all.
    /// </summary>
    /// <remarks>
    /// "Comparable" is doing real work here. A registry that declares no capabilities has not declared that the
    /// server has none — it has said nothing — so every runtime tool would otherwise be reported as unexpected,
    /// which would make the highest-value finding in the feature fire on every listing that omits an optional
    /// field. Silence is not a claim, and a comparison against silence is not a comparison.
    /// </remarks>
    private static List<DiscoveryDrift> Compare(
        DiscoveredResource resource,
        McpServerProbe probe,
        DiscoveryEvidence evidence)
    {
        var drifts = new List<DiscoveryDrift>();
        var declared = resource.DeclaredCapabilityNames;
        var runtime = probe.ToolsOrEmpty.Select(tool => tool.Name).ToArray();

        AddVersionDrift(resource, probe, evidence, drifts);

        if (declared.Count == 0)
        {
            // Nothing to compare names against. The count is still worth stating when the server has tools and
            // the listing mentioned none, because "twelve tools, none listed" is the reviewer's cue to ask why.
            if (runtime.Length > 0)
            {
                drifts.Add(new DiscoveryDrift(
                    DiscoveryDriftKind.ToolCountDrift,
                    resource.Name,
                    "no capabilities declared",
                    $"{runtime.Length} tools",
                    evidence));
            }

            return drifts;
        }

        foreach (var tool in runtime.Except(declared, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            drifts.Add(new DiscoveryDrift(
                DiscoveryDriftKind.UnexpectedRuntimeTool,
                tool,
                null,
                tool,
                evidence));
        }

        // Suppressed when the tool list was cut short: a tool absent from an incomplete read is a tool that might
        // be on the page nobody got to. Reporting it would be a finding produced by SkillForge's own bound.
        if (probe.Paging is null or { IsComplete: true })
        {
            foreach (var tool in declared.Except(runtime, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                drifts.Add(new DiscoveryDrift(
                    DiscoveryDriftKind.DeclaredToolMissing,
                    tool,
                    tool,
                    null,
                    evidence));
            }
        }

        return drifts;
    }

    /// <summary>
    /// Compares the version the registry names with the one the server reports about itself.
    /// </summary>
    /// <remarks>
    /// Only when **both** sides said something. A listing with no version has not claimed the server is
    /// unversioned, and a server that reports no <c>serverInfo.version</c> has not contradicted the listing.
    /// Comparison is exact and ordinal: <c>2.1.0</c> against <c>2.1</c> is a difference worth showing, because
    /// deciding those are the same version means picking a version grammar the two sides never agreed on.
    /// </remarks>
    private static void AddVersionDrift(
        DiscoveredResource resource,
        McpServerProbe probe,
        DiscoveryEvidence evidence,
        List<DiscoveryDrift> drifts)
    {
        if (resource.Version is not { Length: > 0 } declared
            || probe.SelfReportedVersion is not { Length: > 0 } runtime
            || string.Equals(declared, runtime, StringComparison.Ordinal))
        {
            return;
        }

        drifts.Add(new DiscoveryDrift(
            DiscoveryDriftKind.SelfReportedVersionDrift,
            resource.Name,
            declared,
            runtime,
            evidence));
    }

    /// <summary>
    /// Turns the differences into findings.
    /// </summary>
    /// <remarks>
    /// Severities, and why each is what it is:
    ///
    /// <see cref="DiagnosticCodes.DiscoveryUnexpectedRuntimeTool"/> is a **Warning**. It was measured before it
    /// was published — the numbers are in <c>docs/validation-rules.md</c> — and the reason it is not an Error is
    /// that a registry listing may legitimately be a summary rather than a manifest. The reason it is not Info is
    /// that this is the exact shape a capability nobody reviewed takes when it arrives.
    ///
    /// The other three are **Info**. A missing declared tool is usually a stale listing; an endpoint that
    /// redirected is usually a CDN; a count that differs with nothing to compare it against is a prompt to look,
    /// not a defect. None of them is a reason to fail a build by default.
    /// </remarks>
    private static IEnumerable<Diagnostic> Findings(ResourceVerification verification)
    {
        foreach (var drift in verification.Drifts)
        {
            yield return drift.Kind switch
            {
                DiscoveryDriftKind.UnexpectedRuntimeTool => Diagnostic.Warning(
                    DiagnosticCodes.DiscoveryUnexpectedRuntimeTool,
                    $"'{verification.Resource.Name}' answered with a tool the registry did not declare: "
                        + $"'{drift.Subject}'.",
                    drift.Evidence.Registry,
                    suggestion: "The registry's listing is a claim; the tool list came from the server. A "
                        + "capability that is running and was never listed is one nobody reviewed. Check it "
                        + "against the policy's MCP rules — this finding describes the difference and does not "
                        + "decide whether it is allowed."),

                DiscoveryDriftKind.DeclaredToolMissing => Diagnostic.Info(
                    DiagnosticCodes.DiscoveryDeclaredToolMissing,
                    $"'{verification.Resource.Name}' declares '{drift.Subject}' in the registry, and the server "
                        + "did not answer with it.",
                    drift.Evidence.Registry,
                    suggestion: "Usually a listing that has fallen behind the server. Worth knowing because a "
                        + "skill written against the listing will call a tool that is not there."),

                DiscoveryDriftKind.SelfReportedVersionDrift => Diagnostic.Info(
                    DiagnosticCodes.DiscoverySelfReportedVersionDrift,
                    $"'{verification.Resource.Name}': the registry names version '{drift.Declared}' and the "
                        + $"server reports '{drift.Runtime}' about itself.",
                    drift.Evidence.Registry,
                    suggestion: "Two claims that disagree, not one correcting the other: serverInfo.version is "
                        + "self-reported and is not verified by the protocol. Worth knowing because whichever is "
                        + "wrong, the version somebody reviewed is not the version answering."),

                // Every kind that reaches here is a count difference. EndpointDrift is never produced — see the
                // remarks on DiscoveryDriftKind — so it has no code and cannot arrive.
                _ => Diagnostic.Info(
                    DiagnosticCodes.DiscoveryToolCountDrift,
                    $"'{verification.Resource.Name}': the registry says {drift.Declared} and the server answered "
                        + $"{drift.Runtime}.",
                    drift.Evidence.Registry,
                    suggestion: "The names could not be compared one to one, so only the counts are stated. A "
                        + "listing that declares no capabilities has said nothing about them rather than saying "
                        + "there are none."),
            };
        }
    }

    private static string Unsupported(DiscoveredResource resource) => resource.Type switch
    {
        DiscoveredResourceType.McpServer when resource.Endpoint is null =>
            "the listing names no endpoint, so there is nothing to ask",

        DiscoveredResourceType.McpServer =>
            $"the endpoint is '{resource.Endpoint!.Scheme}', and only http and https are asked; SkillForge never "
                + "launches a local server to inspect it",

        _ =>
            $"a {resource.Type} cannot be asked what it exposes without running it, which this command does not do",
    };
}
