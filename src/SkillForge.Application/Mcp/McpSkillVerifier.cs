using System.Text;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Skills;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Verifies one MCP-served skill: its manifest, the bytes of every file it names, its frontmatter, and its boundary.
/// </summary>
/// <remarks>
/// The order is the specification's. <c>skills/get</c> supplies the manifest; a malformed manifest stops everything,
/// because a host must not load on it; a dynamic one stops before any read, because there is nothing to compare
/// against and no baseline is fabricated; a static one has every file read, hashed and measured. Only once the
/// <c>SKILL.md</c> bytes have matched is its frontmatter parsed — a host must not use unverified content, and a
/// comparison against content that already failed would be noise on top of the finding that matters.
///
/// The directory listing, when the server offers one, is compared against the manifest and never added to it. A file
/// it shows that the manifest does not name is reported and is not read.
/// </remarks>
public sealed class McpSkillVerifier
{
    /// <summary>
    /// The most directories one skill's tree is walked through. Each costs at least one request, and up to eight when
    /// it pages, so this is what bounds the walk; a tree deeper or wider than this is reported as partially enumerated.
    /// </summary>
    public const int MaxDirectories = 64;

    /// <summary>
    /// The most directory entries one skill's walk keeps. Twice the specification's per-skill file limit, so a
    /// conforming skill plus its directories always fits.
    /// </summary>
    public const int MaxEntries = McpSkillManifestRules.MaxResources * 2;

    private const string SkillFileSuffix = "/SKILL.md";

    private readonly IMcpSkillsClient _client;
    private readonly IFrontmatterParser _parser;
    private readonly IHashCalculator _hasher;
    private readonly TimeProvider _time;

    /// <summary>Initialises the verifier.</summary>
    /// <param name="client">Speaks the skills extension.</param>
    /// <param name="parser">The one frontmatter parser, shared with the skill loader.</param>
    /// <param name="hasher">Computes SHA-256.</param>
    /// <param name="time">Stamps provenance with when the manifest was read.</param>
    public McpSkillVerifier(
        IMcpSkillsClient client,
        IFrontmatterParser parser,
        IHashCalculator hasher,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(time);

        _client = client;
        _parser = parser;
        _hasher = hasher;
        _time = time;
    }

    /// <summary>Verifies one skill.</summary>
    /// <param name="server">The HTTP server serving it.</param>
    /// <param name="probe">What the server said about itself, for provenance.</param>
    /// <param name="skillUri">The skill's <c>SKILL.md</c> URI.</param>
    /// <param name="listed">Whether <c>skills/list</c> carried it.</param>
    /// <param name="directoryRead">Whether the server declared <c>directoryRead</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The verification and the findings it produced.</returns>
    public async Task<(McpSkillVerification Verification, IReadOnlyList<Diagnostic> Findings)> VerifyAsync(
        McpServerDeclaration server,
        McpServerProbe probe,
        string skillUri,
        bool listed,
        bool directoryRead,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentException.ThrowIfNullOrEmpty(skillUri);

        var get = await _client.GetAsync(server, skillUri, cancellationToken).ConfigureAwait(false);

        if (get.Value is not { } entry)
        {
            return (
                new McpSkillVerification(
                    skillUri,
                    null,
                    listed,
                    McpSkillIntegrityStatus.Unavailable,
                    [],
                    [],
                    [],
                    "not attempted",
                    null,
                    Detail: $"skills/get did not answer: {get.Error}"),
                []);
        }

        var basis = new McpSkillVerification(
            skillUri,
            Parse(entry.FrontmatterJson, entry.Uri)?.Name,
            listed,
            McpSkillIntegrityStatus.IntegrityMatch,
            [],
            [],
            [],
            "not attempted",
            Provenance(server, probe, entry),
            entry.TtlMs,
            entry.CacheScope);

        var problems = McpSkillManifestRules.Problems(entry);

        if (problems.Count > 0)
        {
            return (
                basis with { Status = McpSkillIntegrityStatus.InvalidManifest, Detail = string.Join("; ", problems) },
                [Finding(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.McpSkillInvalidManifest,
                    server,
                    skillUri,
                    $"has a manifest a host must not load: {string.Join("; ", problems)}.")]);
        }

        if (entry.Manifest.Kind == McpSkillManifestKind.Dynamic)
        {
            return (
                basis with { Status = McpSkillIntegrityStatus.NotEstablished },
                [Finding(
                    DiagnosticSeverity.Info,
                    DiagnosticCodes.McpSkillDynamicIntegrityLimit,
                    server,
                    skillUri,
                    "serves dynamic resources. Static content integrity cannot be established.")]);
        }

        return await VerifyStaticAsync(server, entry, basis, directoryRead, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(McpSkillVerification Verification, IReadOnlyList<Diagnostic> Findings)> VerifyStaticAsync(
        McpServerDeclaration server,
        McpSkillEntry entry,
        McpSkillVerification basis,
        bool directoryRead,
        CancellationToken cancellationToken)
    {
        var findings = new List<Diagnostic>();
        var checks = new List<McpSkillResourceCheck>();
        ReadOnlyMemory<byte>? skillFile = null;

        foreach (var resource in entry.Manifest.Resources)
        {
            var read = await _client.ReadAsync(server, resource.Uri, cancellationToken).ConfigureAwait(false);
            var check = Check(resource, read);

            checks.Add(check);
            findings.AddRange(ContentFindings(server, entry.Uri, check));

            if (check.Matches && string.Equals(resource.Uri, entry.Uri, StringComparison.Ordinal))
            {
                skillFile = read.Value!.Bytes;
            }
        }

        var drift = skillFile is { } verified ? FrontmatterDrift(entry, verified) : [];

        if (drift.Count > 0)
        {
            findings.Add(Finding(
                DiagnosticSeverity.Error,
                DiagnosticCodes.McpSkillFrontmatterDrift,
                server,
                entry.Uri,
                $"describes frontmatter its SKILL.md does not contain — {string.Join("; ", drift)}."));
        }

        var (unlisted, enumeration) = await UnlistedAsync(server, entry, directoryRead, cancellationToken)
            .ConfigureAwait(false);

        findings.AddRange(unlisted.Select(uri => Finding(
            DiagnosticSeverity.Warning,
            DiagnosticCodes.McpSkillUnlistedResource,
            server,
            entry.Uri,
            $"has '{uri}' in its directory but not in its manifest. It was not read and is not part of the approved set.")));

        return (
            basis with
            {
                Status = findings.Count > 0
                    ? McpSkillIntegrityStatus.IntegrityMismatch
                    : McpSkillIntegrityStatus.IntegrityMatch,
                Resources = checks,
                FrontmatterDrift = drift,
                UnlistedResources = unlisted,
                DirectoryEnumeration = enumeration,
            },
            findings);
    }

    private McpSkillResourceCheck Check(McpSkillResource resource, McpResponse<McpResourceContent> read)
    {
        if (read.Value is not { } content)
        {
            return new McpSkillResourceCheck(resource.Uri, resource.Digest, resource.Size, null, null, read.Error);
        }

        return new McpSkillResourceCheck(
            resource.Uri,
            resource.Digest,
            resource.Size,
            McpSkillManifestRules.DigestPrefix + _hasher.ComputeSha256(content.Bytes.Span),
            content.Bytes.Length);
    }

    /// <summary>
    /// One finding per file at most. A size that differs almost always comes with a digest that differs, and reporting
    /// both would describe one changed file twice; the size is the more legible of the two, so it wins.
    /// </summary>
    private static IEnumerable<Diagnostic> ContentFindings(
        McpServerDeclaration server,
        string skillUri,
        McpSkillResourceCheck check)
    {
        if (check.ActualDigest is null)
        {
            yield return Finding(
                DiagnosticSeverity.Error,
                DiagnosticCodes.McpSkillMissingResource,
                server,
                skillUri,
                $"names '{check.Uri}' in its manifest, and it could not be read: {check.Detail}.");
        }
        else if (check.ActualSize != check.DeclaredSize)
        {
            yield return Finding(
                DiagnosticSeverity.Error,
                DiagnosticCodes.McpSkillSizeMismatch,
                server,
                skillUri,
                $"declares '{check.Uri}' as {check.DeclaredSize} bytes, and {check.ActualSize} arrived.");
        }
        else if (!check.Matches)
        {
            yield return Finding(
                DiagnosticSeverity.Error,
                DiagnosticCodes.McpSkillDigestMismatch,
                server,
                skillUri,
                $"declares '{check.Uri}' as {check.DeclaredDigest}, and the bytes that arrived are {check.ActualDigest}.");
        }
    }

    private IReadOnlyList<string> FrontmatterDrift(McpSkillEntry entry, ReadOnlyMemory<byte> skillFile)
    {
        var described = Parse(entry.FrontmatterJson, entry.Uri);

        if (described is null)
        {
            return ["frontmatter: the described frontmatter could not be read"];
        }

        var split = FrontmatterSplitter.TrySplit(Encoding.UTF8.GetString(skillFile.Span));

        if (split is null)
        {
            return ["frontmatter: SKILL.md has no frontmatter block"];
        }

        var served = _parser.Parse(split.Yaml, split.StartLine, entry.Uri);

        return served.Value is { } parsed
            ? McpSkillFrontmatterComparer.Differences(described, parsed)
            : ["frontmatter: the SKILL.md frontmatter could not be parsed"];
    }

    /// <summary>
    /// Parses a described frontmatter. JSON is valid YAML, so the parser that reads <c>SKILL.md</c> reads this too —
    /// one pipeline, and two frontmatters that say the same thing come out as the same model.
    /// </summary>
    private SkillFrontmatter? Parse(string? frontmatterJson, string uri) =>
        frontmatterJson is null ? null : _parser.Parse(frontmatterJson, 0, uri).Value;

    private async Task<(IReadOnlyList<string> Unlisted, string Enumeration)> UnlistedAsync(
        McpServerDeclaration server,
        McpSkillEntry entry,
        bool directoryRead,
        CancellationToken cancellationToken)
    {
        if (!directoryRead)
        {
            return ([], "not declared");
        }

        var root = Root(entry.Uri);
        var approved = entry.Manifest.Resources.Select(resource => resource.Uri).ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<string>([root]);
        var visited = new HashSet<string>(StringComparer.Ordinal) { root };
        var unlisted = new SortedSet<string>(StringComparer.Ordinal);
        var complete = true;
        var seen = 0;

        while (pending.TryDequeue(out var directory))
        {
            if (seen >= MaxEntries)
            {
                complete = false;
                break;
            }

            var listing = await _client.ReadDirectoryAsync(server, directory, cancellationToken).ConfigureAwait(false);

            if (listing.Value is not { } children)
            {
                complete = false;
                continue;
            }

            complete &= children.Outcome == McpToolPagingOutcome.Complete;
            seen += children.Entries.Count;

            // A child outside the subtree, or with an empty, '.' or '..' segment, is not followed — and the
            // enumeration stops calling itself complete, because something the server listed was not looked at.
            complete &= children.Entries.All(child => IsInside(child.Uri, root));

            foreach (var child in children.Entries.Where(child => IsInside(child.Uri, root)))
            {
                if (!child.IsDirectory)
                {
                    if (!approved.Contains(child.Uri))
                    {
                        unlisted.Add(child.Uri);
                    }
                }
                else if (!visited.Contains(child.Uri))
                {
                    complete &= visited.Count < MaxDirectories;

                    if (visited.Count < MaxDirectories && visited.Add(child.Uri))
                    {
                        pending.Enqueue(child.Uri);
                    }
                }
            }
        }

        return ([.. unlisted], complete ? "complete" : "partial");
    }

    /// <summary>
    /// The skill root: the <c>SKILL.md</c> URI without its file name, which the specification says has no trailing
    /// slash.
    /// </summary>
    private static string Root(string skillUri) =>
        skillUri.EndsWith(SkillFileSuffix, StringComparison.Ordinal)
            ? skillUri[..^SkillFileSuffix.Length]
            : skillUri[..Math.Max(skillUri.LastIndexOf('/'), 0)];

    /// <summary>
    /// Whether a child the server listed is inside the skill's own subtree. Anything else — another skill, another
    /// origin, or a path with a <c>.</c> or <c>..</c> segment — is ignored rather than followed: a directory listing
    /// is not allowed to widen what is looked at.
    /// </summary>
    private static bool IsInside(string uri, string root) =>
        uri.StartsWith(root + "/", StringComparison.Ordinal)
        && !uri[(root.Length + 1)..].Split('/').Any(segment => segment is "." or ".." or "");

    private McpSkillProvenance Provenance(McpServerDeclaration server, McpServerProbe probe, McpSkillEntry entry) =>
        new(
            server.Name,
            UrlRedaction.WithoutCredentials(server.Command),
            probe.SelfReportedName,
            probe.SelfReportedVersion,
            probe.AnsweredRevision,
            entry.Uri,
            entry.Manifest.Resources,
            _time.GetUtcNow());

    private static Diagnostic Finding(
        DiagnosticSeverity severity,
        string code,
        McpServerDeclaration server,
        string skillUri,
        string message) =>
        new(code, severity, $"'{server.Name}' skill '{skillUri}' {message}", server.SourcePath);
}
