using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Asks the servers an MCP configuration declares about the skills they serve, through the
/// <c>io.modelcontextprotocol/skills</c> extension.
/// </summary>
/// <remarks>
/// Built on the inspection that already exists: the file is read by the same readers, and each server is probed by the
/// same <see cref="McpProber"/>, so a server's capabilities — and whether it declared the extension at all — come from
/// the one place that already answers that. There is no second probing stack.
///
/// Nothing is asked without <c>probe</c>. A stdio server is never launched. A server is asked about skills only after
/// it declared the extension in the <c>2026-07-28</c> dialect the extension is defined against, and
/// <c>resources/directory/read</c> only after it declared <c>directoryRead</c>.
/// </remarks>
public sealed class McpSkillsInspector
{
    /// <summary>The extension's identifier.</summary>
    public const string ExtensionIdentifier = "io.modelcontextprotocol/skills";

    /// <summary>The extension setting that permits <c>resources/directory/read</c>.</summary>
    public const string DirectoryReadSetting = "directoryRead";

    /// <summary>The base protocol revision the extension targets.</summary>
    public const string RequiredRevision = "2026-07-28";

    /// <summary>
    /// The most skills verified on one server in one run.
    /// </summary>
    /// <remarks>
    /// ponytail: a count bound, not a byte budget. Each skill is already capped at the specification's 16 MiB, so the
    /// worst case is bounded; a per-server byte budget is the upgrade if a real server makes this too generous.
    /// </remarks>
    public const int MaxSkillsVerifiedPerServer = 100;

    private readonly McpFileInspector _files;
    private readonly IMcpSkillsClient _client;
    private readonly McpSkillVerifier _verifier;

    /// <summary>Initialises the inspector.</summary>
    /// <param name="files">Reads the configuration and probes its servers.</param>
    /// <param name="client">Speaks the skills extension.</param>
    /// <param name="verifier">Verifies one skill.</param>
    public McpSkillsInspector(McpFileInspector files, IMcpSkillsClient client, McpSkillVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(verifier);

        _files = files;
        _client = client;
        _verifier = verifier;
    }

    /// <summary>Lists, and optionally verifies, the skills every declared server serves.</summary>
    /// <param name="path">The MCP configuration file.</param>
    /// <param name="probe">Whether to ask anything at all.</param>
    /// <param name="verify">Whether to fetch and verify each skill, rather than only listing them.</param>
    /// <param name="skillUris">
    /// Skills to verify by URI in addition to the listed ones, on every server that declares the extension. A skill need
    /// not be listed to be served.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The report.</returns>
    public async Task<McpSkillsReport> InspectAsync(
        string path,
        bool probe,
        bool verify,
        IReadOnlyList<string> skillUris,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(skillUris);

        var inspection = await _files.InspectAsync(path, probe, cancellationToken).ConfigureAwait(false);

        var findings = inspection.Diagnostics
            .Where(finding => finding.Code == DiagnosticCodes.ProviderConfigurationNotParsable)
            .ToList();

        var servers = new List<McpSkillsServerReport>(inspection.Servers.Count);

        foreach (var server in inspection.Servers)
        {
            var probed = inspection.Probes.FirstOrDefault(candidate =>
                string.Equals(candidate.ServerName, server.Name, StringComparison.Ordinal));

            var (report, serverFindings) = await ServerAsync(server, probed, probe, verify, skillUris, cancellationToken)
                .ConfigureAwait(false);

            servers.Add(report);
            findings.AddRange(serverFindings);
        }

        return new McpSkillsReport(path, probe, servers, DiagnosticOrdering.Sort(findings));
    }

    private async Task<(McpSkillsServerReport Report, IReadOnlyList<Diagnostic> Findings)> ServerAsync(
        McpServerDeclaration server,
        McpServerProbe? probe,
        bool probed,
        bool verify,
        IReadOnlyList<string> skillUris,
        CancellationToken cancellationToken)
    {
        var endpoint = server.Transport == McpTransport.Http ? UrlRedaction.WithoutCredentials(server.Command) : null;
        var notAsked = NotAskedBecause(server, probe, probed);

        if (notAsked is not null)
        {
            return (new McpSkillsServerReport(server.Name, endpoint, false, false, null, [], notAsked), []);
        }

        var extension = probe!.Extension(ExtensionIdentifier)!;
        var directoryRead = extension.Enables(DirectoryReadSetting);
        var listing = await _client.ListAsync(server, cancellationToken).ConfigureAwait(false);
        var findings = new List<Diagnostic>();

        if (!listing.IsComplete)
        {
            findings.Add(Diagnostic.Info(
                DiagnosticCodes.McpSkillsPagingIncomplete,
                $"'{server.Name}' skills/list was not read to the end ({Why(listing)}), so the skills reported are a "
                    + "floor, not the server's whole inventory.",
                server.SourcePath,
                suggestion: "A skill missing from a listing can still be verified by URI with --skill."));
        }

        var verifications = new List<McpSkillVerification>();
        var detail = default(string);

        if (verify)
        {
            var targets = Targets(listing, skillUris);

            if (targets.Count > MaxSkillsVerifiedPerServer)
            {
                detail = $"verified the first {MaxSkillsVerifiedPerServer} of {targets.Count} skills, by URI";
                targets = [.. targets.Take(MaxSkillsVerifiedPerServer)];
            }

            foreach (var (uri, listed) in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (verification, skillFindings) = await _verifier
                    .VerifyAsync(server, probe, uri, listed, directoryRead, cancellationToken)
                    .ConfigureAwait(false);

                verifications.Add(verification);
                findings.AddRange(skillFindings);
            }
        }

        return (
            new McpSkillsServerReport(server.Name, endpoint, true, directoryRead, listing, verifications, detail),
            findings);
    }

    /// <summary>
    /// Why a server is not asked about skills, or <see langword="null"/> when it is. Every reason is stated, so a
    /// reader can tell "serves no skills" from "was never asked".
    /// </summary>
    private static string? NotAskedBecause(McpServerDeclaration server, McpServerProbe? probe, bool probed)
    {
        if (server.Transport != McpTransport.Http)
        {
            return "not an HTTP server; SkillForge never launches a local command to inspect it";
        }

        if (!probed)
        {
            return "--probe was not given, so nothing was asked";
        }

        if (probe is not { Status: McpProbeStatus.Answered })
        {
            return $"the server did not answer discovery ({probe?.Status.ToString() ?? "not probed"}"
                + (probe?.Detail is { } why ? $": {why})" : ")");
        }

        if (!string.Equals(probe.AnsweredRevision, RequiredRevision, StringComparison.Ordinal))
        {
            return $"the server answered in {probe.AnsweredRevision ?? "an unknown revision"}; the skills extension "
                + $"is defined against {RequiredRevision}";
        }

        return probe.Extension(ExtensionIdentifier) is null
            ? $"the server does not declare {ExtensionIdentifier}"
            : null;
    }

    /// <summary>
    /// The listed skills and the named ones, once each, ordered by URI so two runs verify in the same order.
    /// </summary>
    private static List<(string Uri, bool Listed)> Targets(McpSkillListing listing, IReadOnlyList<string> skillUris)
    {
        var listed = listing.Skills.Select(skill => skill.Uri).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. listed
                .Union(skillUris.Where(uri => uri.Length > 0), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(uri => (uri, listed.Contains(uri))),
        ];
    }

    private static string Why(McpSkillListing listing) => listing switch
    {
        { Detail: { } error } => $"a page failed: {error}",
        { Outcome: McpToolPagingOutcome.CursorLoopDetected } => "the server repeated a cursor",
        _ => $"SkillForge stopped at its {listing.PagesRead}-page limit",
    };
}
