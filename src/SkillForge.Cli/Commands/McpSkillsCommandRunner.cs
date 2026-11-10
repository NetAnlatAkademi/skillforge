using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Mcp;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Mcp.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge mcp skills inspect</c> and <c>mcp skills verify</c> do.
/// </summary>
/// <remarks>
/// <c>inspect</c> describes: which servers declare the skills extension, and what <c>skills/list</c> says they serve.
/// It exits zero whatever it finds. <c>verify</c> fetches every listed skill — and any named with <c>--skill</c> — and
/// checks each file's bytes against the manifest; it reports evidence and fails only under <c>--fail-on-mismatch</c>.
///
/// **"Integrity match" is used narrowly, and every output says so.** It means the bytes that arrived are the bytes the
/// manifest described. Digests are unsigned and served by the same server as the content, so it says nothing about
/// trust, safety or who published the skill.
///
/// SARIF carries only the integrity findings, <c>SF8301</c>–<c>SF8306</c>. A dynamic skill and an incomplete listing
/// are statements about what could be checked; an annotation on a pull request for either would be noise.
/// </remarks>
internal sealed class McpSkillsCommandRunner
{
    private const string IntegrityMeans =
        "every manifest file's bytes matched its declared sha256 digest and size, and the frontmatter agreed; "
            + "not trust, not safety, not a verified publisher";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly McpSkillsInspector _inspector;
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    /// <summary>Initialises the runner.</summary>
    /// <param name="inspector">Lists and verifies served skills.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="serializers">Report serializers, for the SARIF path.</param>
    public McpSkillsCommandRunner(
        McpSkillsInspector inspector,
        IFileSystem fileSystem,
        IEnumerable<IValidationReportSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(serializers);

        _inspector = inspector;
        _fileSystem = fileSystem;
        _serializers = [.. serializers];
    }

    /// <summary>Lists, and under <c>verify</c> verifies, the skills a configuration's servers serve.</summary>
    /// <param name="request">What to do and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(McpSkillsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await _inspector
            .InspectAsync(request.Path, request.Probe, request.Verify, request.SkillUris, cancellationToken)
            .ConfigureAwait(false);

        var content = request.Format switch
        {
            OutputFormat.Json => ToJson(report),
            OutputFormat.Sarif => Sarif(report),
            _ => Printable(ToText(request, report)),
        };

        await WriteAsync(content, request.OutputPath, cancellationToken).ConfigureAwait(false);

        if (report.Diagnostics.Any(finding => finding.Code == DiagnosticCodes.ProviderConfigurationNotParsable))
        {
            return ExitCodes.ValidationFailed;
        }

        return request.Verify && request.FailOnMismatch && report.HasIntegrityFindings
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    /// <summary>
    /// The console text without control characters other than line breaks. URIs, error messages and frontmatter
    /// values come from the server, and an escape sequence in one of them would otherwise reach the terminal.
    /// JSON and SARIF escape these on their own.
    /// </summary>
    private static string Printable(string text) =>
        string.Concat(text.Where(character => character is '\n' or '\r' || !char.IsControl(character)));

    private static string ToText(McpSkillsRequest request, McpSkillsReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine(request.Verify ? "SkillForge MCP Skills Verify" : "SkillForge MCP Skills Inspect");
        builder.AppendLine();
        builder.AppendLine($"File:    {report.Path}");
        builder.AppendLine($"Servers: {report.Servers.Count}");

        if (!report.Probed)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Nothing was asked. Add --probe for that; only http and https MCP servers are ever asked, and a local "
                    + "stdio server is never launched.");
        }

        foreach (var server in report.Servers)
        {
            AppendServer(builder, server);
        }

        AppendFindings(builder, report.Diagnostics);

        if (request.Verify)
        {
            builder.AppendLine();
            builder.AppendLine(
                "'Integrity match' means the bytes that arrived are the bytes the manifest described. It does NOT mean "
                    + "trusted, it does NOT mean safe, and it does NOT mean a verified publisher: the digests are "
                    + "unsigned and come from the same server as the content.");
        }

        return builder.ToString();
    }

    private static void AppendServer(StringBuilder builder, McpSkillsServerReport server)
    {
        builder.AppendLine();
        builder.AppendLine($"  {server.ServerName}");
        builder.AppendLine($"      endpoint: {server.Endpoint ?? "(not an HTTP server)"}");

        if (!server.DeclaresSkills)
        {
            builder.AppendLine($"      skills:   not asked — {server.Detail}");
            return;
        }

        builder.AppendLine(
            $"      skills:   declares {McpSkillsInspector.ExtensionIdentifier} "
            + $"(directoryRead: {(server.DirectoryRead ? "yes" : "no")})");

        if (server.Listing is { } listing)
        {
            builder.AppendLine(
                $"      listed:   {listing.Skills.Count} skills over {listing.PagesRead} pages"
                + (listing.IsComplete ? string.Empty : " — the list was not read to the end")
                + (listing.MalformedEntries > 0 ? $", {listing.MalformedEntries} entries without a uri skipped" : string.Empty));

            foreach (var skill in listing.Skills)
            {
                builder.AppendLine($"        - {skill.Uri}  ({Describe(skill.Manifest)})");
            }
        }

        if (server.Detail is { } detail)
        {
            builder.AppendLine($"      note:     {detail}");
        }

        foreach (var verification in server.Verifications)
        {
            AppendVerification(builder, verification);
        }
    }

    private static void AppendVerification(StringBuilder builder, McpSkillVerification verification)
    {
        builder.AppendLine();
        builder.AppendLine($"      {verification.SkillUri}{(verification.Listed ? string.Empty : "  (not listed; fetched by uri)")}");
        builder.AppendLine($"          status:      {Describe(verification)}");

        if (verification.Resources.Count > 0)
        {
            builder.AppendLine(
                $"          files:       {verification.Resources.Count(check => check.Matches)} of "
                + $"{verification.Resources.Count} matched");
        }

        foreach (var drift in verification.FrontmatterDrift)
        {
            builder.AppendLine($"          drift:       {drift}");
        }

        foreach (var unlisted in verification.UnlistedResources)
        {
            builder.AppendLine($"          unlisted:    {unlisted}");
        }

        builder.AppendLine($"          directory:   {verification.DirectoryEnumeration}");

        if (verification.Provenance is { } provenance)
        {
            builder.AppendLine(
                $"          provenance:  {McpSkillProvenance.Origin} {provenance.Endpoint ?? "(no endpoint)"}, "
                + $"self-reported {provenance.SelfReportedName ?? "(no name)"} {provenance.SelfReportedVersion ?? "(no version)"}, "
                + $"{provenance.ProtocolVersion}, observed {provenance.ObservedAt.ToString("O", CultureInfo.InvariantCulture)}");
        }
    }

    private static string Describe(McpSkillManifest manifest) => manifest.Kind switch
    {
        McpSkillManifestKind.Static => $"{manifest.Resources.Count} files",
        McpSkillManifestKind.Dynamic => "dynamic",
        _ => $"invalid manifest: {manifest.Detail}",
    };

    private static string Describe(McpSkillVerification verification) => verification.Status switch
    {
        McpSkillIntegrityStatus.IntegrityMatch => "integrity match — not a statement about trust or safety",
        McpSkillIntegrityStatus.IntegrityMismatch => "integrity mismatch",
        McpSkillIntegrityStatus.NotEstablished => "not established — dynamic resources have no content baseline",
        McpSkillIntegrityStatus.InvalidManifest => $"invalid manifest — {verification.Detail}",
        _ => $"unavailable — {verification.Detail}",
    };

    private static void AppendFindings(StringBuilder builder, IReadOnlyList<Diagnostic> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Findings:");

        foreach (var finding in findings)
        {
            var mark = finding.Severity switch
            {
                DiagnosticSeverity.Error => "x",
                DiagnosticSeverity.Warning => "!",
                _ => "i",
            };

            builder.AppendLine($"  {mark} {finding.Code} {finding.Message}");
        }
    }

    private static string ToJson(McpSkillsReport report)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["path"] = report.Path,
            ["probed"] = report.Probed,
            ["integrityMeans"] = IntegrityMeans,
            ["servers"] = new JsonArray([.. report.Servers.Select(server => (JsonNode)ServerJson(server))]),
            ["diagnostics"] = new JsonArray([.. report.Diagnostics.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject ServerJson(McpSkillsServerReport server) =>
        new()
        {
            ["name"] = server.ServerName,
            ["endpoint"] = server.Endpoint,
            ["declaresSkills"] = server.DeclaresSkills,
            ["directoryRead"] = server.DirectoryRead,
            ["detail"] = server.Detail,
            ["listing"] = server.Listing is { } listing
                ? new JsonObject
                {
                    ["complete"] = listing.IsComplete,
                    ["pagesRead"] = listing.PagesRead,
                    ["outcome"] = listing.Outcome.ToString(),
                    ["malformedEntries"] = listing.MalformedEntries,
                    ["duplicateEntries"] = listing.DuplicateEntries,
                    ["error"] = listing.Detail,
                    ["skills"] = new JsonArray([.. listing.Skills.Select(skill => (JsonNode)new JsonObject
                    {
                        ["uri"] = skill.Uri,
                        ["resources"] = skill.Manifest.Kind.ToString(),
                        ["resourceCount"] = skill.Manifest.Resources.Count,
                    })]),
                }
                : null,
            ["verifications"] = new JsonArray([.. server.Verifications.Select(item => (JsonNode)VerificationJson(item))]),
        };

    private static JsonObject VerificationJson(McpSkillVerification verification) =>
        new()
        {
            ["uri"] = verification.SkillUri,
            ["name"] = verification.Name,
            ["listed"] = verification.Listed,
            ["status"] = verification.Status.ToString(),
            ["detail"] = verification.Detail,
            ["ttlMs"] = verification.TtlMs,
            ["cacheScope"] = verification.CacheScope,
            ["directoryEnumeration"] = verification.DirectoryEnumeration,
            ["resources"] = new JsonArray([.. verification.Resources.Select(check => (JsonNode)new JsonObject
            {
                ["uri"] = check.Uri,
                ["declaredDigest"] = check.DeclaredDigest,
                ["declaredSize"] = check.DeclaredSize,
                ["actualDigest"] = check.ActualDigest,
                ["actualSize"] = check.ActualSize,
                ["matches"] = check.Matches,
                ["detail"] = check.Detail,
            })]),
            ["frontmatterDrift"] = new JsonArray([.. verification.FrontmatterDrift.Select(drift => (JsonNode)drift)]),
            ["unlistedResources"] = new JsonArray([.. verification.UnlistedResources.Select(uri => (JsonNode)uri)]),
            ["provenance"] = verification.Provenance is { } provenance
                ? new JsonObject
                {
                    ["origin"] = McpSkillProvenance.Origin,
                    ["server"] = provenance.ServerName,
                    ["endpoint"] = provenance.Endpoint,
                    ["selfReportedName"] = provenance.SelfReportedName,
                    ["selfReportedVersion"] = provenance.SelfReportedVersion,
                    ["protocolVersion"] = provenance.ProtocolVersion,
                    ["skillUri"] = provenance.SkillUri,
                    ["serverIdentityVerified"] = false,
                    ["manifest"] = new JsonArray([.. provenance.Manifest.Select(resource => (JsonNode)new JsonObject
                    {
                        ["uri"] = resource.Uri,
                        ["digest"] = resource.Digest,
                        ["size"] = resource.Size,
                    })]),
                    ["observedAt"] = provenance.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
                }
                : null,
        };

    /// <summary>The integrity findings as SARIF, through the existing serializer.</summary>
    private string Sarif(McpSkillsReport report)
    {
        var findings = report.Diagnostics
            .Where(finding => DiagnosticCodes.IsMcpSkillIntegrityFinding(finding.Code)
                || finding.Code == DiagnosticCodes.ProviderConfigurationNotParsable)
            .ToArray();

        return _serializers
            .Single(candidate => string.Equals(candidate.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase))
            .Serialize(new ValidationReport(string.Empty, report.Path, findings, ValidationSummary.FromDiagnostics(findings)));
    }

    private async Task WriteAsync(string content, string? outputPath, CancellationToken cancellationToken)
    {
        if (outputPath is not { Length: > 0 })
        {
            await Console.Out.WriteAsync(content).ConfigureAwait(false);
            return;
        }

        var directory = Path.GetDirectoryName(_fileSystem.GetFullPath(outputPath));
        if (directory is { Length: > 0 })
        {
            _fileSystem.CreateDirectory(directory);
        }

        await _fileSystem.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
    }
}
