using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Discovery;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Discovery;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Everything <c>discovery verify</c> was asked to do.
/// </summary>
/// <param name="Query">What to search the registry for.</param>
/// <param name="Registry">The registry URL to ask. Required.</param>
/// <param name="Kind">Which discovery adapter to use.</param>
/// <param name="Probe">
/// Whether to ask the discovered servers about themselves. Off by default: the search is one network request the
/// user asked for, and probing every result is a different and larger one.
/// </param>
/// <param name="FailOnDrift">Whether a reported difference should fail the run.</param>
/// <param name="Limits">The bounds the registry request is made under.</param>
/// <param name="Format">Console, JSON or SARIF.</param>
/// <param name="OutputPath">File to write to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record DiscoveryVerifyRequest(
    string Query,
    string Registry,
    string Kind,
    bool Probe,
    bool FailOnDrift,
    RemoteDiscoveryLimits Limits,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);

/// <summary>
/// What <c>skillforge discovery verify</c> does.
/// </summary>
/// <remarks>
/// It reads a registry, then — only when asked — asks each remote HTTP MCP server it listed what it actually
/// exposes, and reports the differences.
///
/// **The word "verified" is used narrowly here and the output says so on every path.** No drift means the
/// capabilities a registry declared match the tools a server answered with at the moment it was asked. It does not
/// mean the server is safe, trusted, or worth connecting to; those are policy questions, and policy is a different
/// command on purpose.
///
/// SARIF is offered because drift findings **are** findings: a tool that is running and was never listed is
/// exactly the kind of thing a pull request should carry an annotation for. The search result underneath it is
/// not, which is why <c>discover</c> has no SARIF.
/// </remarks>
internal sealed class DiscoveryVerifyCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IReadOnlyList<IRemoteResourceDiscoveryAdapter> _adapters;
    private readonly DiscoveryVerifier _verifier;
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<IValidationReportSerializer> _serializers;

    /// <summary>Initialises the runner.</summary>
    /// <param name="adapters">One adapter per kind of registry.</param>
    /// <param name="verifier">Compares each listing against what its server answers.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    /// <param name="serializers">Report serializers, for the SARIF path.</param>
    public DiscoveryVerifyCommandRunner(
        IEnumerable<IRemoteResourceDiscoveryAdapter> adapters,
        DiscoveryVerifier verifier,
        IFileSystem fileSystem,
        IEnumerable<IValidationReportSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(serializers);

        _adapters = [.. adapters];
        _verifier = verifier;
        _fileSystem = fileSystem;
        _serializers = [.. serializers];
    }

    /// <summary>Searches a registry and verifies what can be verified.</summary>
    /// <param name="request">What to verify and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(
        DiscoveryVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Registry.Length == 0)
        {
            await Console.Error
                .WriteLineAsync(
                    "Usage error:\nNo registry was specified. Discovery has no default registry: SkillForge "
                        + "never reaches a registry nobody named.")
                .ConfigureAwait(false);

            return ExitCodes.InvalidUsage;
        }

        if (!Uri.TryCreate(request.Registry, UriKind.Absolute, out var registry)
            || (registry.Scheme != Uri.UriSchemeHttp && registry.Scheme != Uri.UriSchemeHttps))
        {
            await Console.Error
                .WriteLineAsync($"Usage error:\n'{request.Registry}' is not an http or https registry URL.")
                .ConfigureAwait(false);

            return ExitCodes.InvalidUsage;
        }

        var adapter = _adapters.FirstOrDefault(candidate =>
            string.Equals(candidate.Kind, request.Kind, StringComparison.OrdinalIgnoreCase));

        if (adapter is null)
        {
            await Console.Error
                .WriteLineAsync(
                    $"Usage error:\nNo discovery adapter is registered for '{request.Kind}'. Available: "
                        + $"{string.Join(", ", _adapters.Select(candidate => candidate.Kind))}.")
                .ConfigureAwait(false);

            return ExitCodes.InvalidUsage;
        }

        var discovered = await adapter
            .SearchAsync(
                new RemoteDiscoveryRequest(registry, request.Query, DiscoveredResourceType.McpServer, request.Limits),
                cancellationToken)
            .ConfigureAwait(false);

        var report = await _verifier
            .VerifyAsync(discovered, request.Query, request.Probe, cancellationToken)
            .ConfigureAwait(false);

        await WriteAsync(request, report, cancellationToken).ConfigureAwait(false);

        if (report.Diagnostics.Any(finding => finding.Code == DiagnosticCodes.DiscoveryResponseNotUsable
            && finding.Severity >= DiagnosticSeverity.Warning))
        {
            return ExitCodes.ValidationFailed;
        }

        return request.FailOnDrift && report.HasDrift ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    private static string ToText(DiscoveryVerifyRequest request, DiscoveryVerificationReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Discovery Verify");
        builder.AppendLine();
        builder.AppendLine($"Registry:  {report.Registry}");
        builder.AppendLine($"Query:     {(report.Query.Length > 0 ? report.Query : "(everything)")}");
        builder.AppendLine($"Resources: {report.Resources.Count}");
        builder.AppendLine($"Probed:    {report.ProbedCount}");

        if (!request.Probe)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Nothing was asked about itself. Add --probe for that; only remote http and https MCP servers "
                    + "are ever asked, and a local stdio server is never launched.");
        }

        foreach (var resource in report.Resources)
        {
            AppendResource(builder, resource);
        }

        if (report.Resources.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("  (the registry listed no MCP server this adapter could read)");
        }

        AppendFindings(builder, report);

        builder.AppendLine();
        builder.AppendLine(
            "'Verified, no drift' means the declared capabilities matched the tools the server answered with, "
                + "at the moment it was asked. It does NOT mean trusted, and it does NOT mean safe.");
        builder.AppendLine(
            "This report describes differences. Whether a difference is permitted is decided by "
                + "'skillforge policy check', not here.");

        return builder.ToString();
    }

    private static void AppendResource(StringBuilder builder, ResourceVerification resource)
    {
        builder.AppendLine();
        builder.AppendLine($"  {resource.Resource.Name}");
        builder.AppendLine($"      endpoint: {resource.Resource.Endpoint?.ToString() ?? "(none declared)"}");
        builder.AppendLine($"      status:   {Describe(resource)}");
        builder.AppendLine(
            $"      declared: {resource.Resource.Capabilities.Count} capabilities (by the registry)");

        if (resource.Status is DiscoveryVerificationStatus.VerifiedNoDrift
            or DiscoveryVerificationStatus.DriftDetected)
        {
            builder.AppendLine(
                $"      runtime:  {resource.RuntimeTools.Count} tools (from the server's own tools/list)"
                + (resource.Paging is { IsComplete: false } ? " — the list was not read to the end" : string.Empty));
        }

        foreach (var drift in resource.Drifts)
        {
            builder.AppendLine($"      drift:    {drift.Kind} — {drift.Subject}");
            builder.AppendLine(
                $"                declared: {drift.Declared ?? "(nothing)"} · runtime: "
                + $"{drift.Runtime ?? "(nothing)"}");
            builder.AppendLine(
                $"                read from {drift.Evidence.Registry} and {drift.Evidence.Endpoint}"
                + (drift.Evidence.ProbedRevision is { } revision ? $" ({revision})" : string.Empty));
        }
    }

    private static string Describe(ResourceVerification resource) => resource.Status switch
    {
        DiscoveryVerificationStatus.VerifiedNoDrift =>
            "verified, no drift — not a statement about trust or safety",

        DiscoveryVerificationStatus.DriftDetected =>
            $"drift detected ({resource.Drifts.Count})",

        DiscoveryVerificationStatus.ProbeFailed => $"probe failed — {resource.Detail}",

        DiscoveryVerificationStatus.UnsupportedResource => $"not verifiable — {resource.Detail}",

        _ => $"not probed — {resource.Detail}",
    };

    private static void AppendFindings(StringBuilder builder, DiscoveryVerificationReport report)
    {
        if (report.Diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Findings:");

        foreach (var finding in report.Diagnostics)
        {
            builder.AppendLine($"  {Mark(finding.Severity)} {finding.Code} {finding.Message}");
        }
    }

    private static string Mark(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "x",
        DiagnosticSeverity.Warning => "!",
        _ => "i",
    };

    private static string ToJson(DiscoveryVerificationReport report)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["registry"] = report.Registry,
            ["query"] = report.Query,
            ["hasDrift"] = report.HasDrift,
            ["probedCount"] = report.ProbedCount,
            ["resources"] = new JsonArray([.. report.Resources.Select(resource => (JsonNode)new JsonObject
            {
                ["id"] = resource.Resource.Id,
                ["name"] = resource.Resource.Name,
                ["endpoint"] = resource.Resource.Endpoint?.ToString(),
                ["status"] = resource.Status.ToString(),

                // Spelled out in the payload, because "VerifiedNoDrift" is the one value in this report a
                // consumer is likely to read as "safe".
                ["verifiedMeans"] = "declared capabilities matched the runtime tool list when asked; not trust",
                ["detail"] = resource.Detail,
                ["declaredCapabilities"] = new JsonArray(
                    [.. resource.Resource.DeclaredCapabilityNames.Select(name => (JsonNode)name!)]),
                ["runtimeTools"] = new JsonArray(
                    [.. resource.RuntimeTools.Select(tool => (JsonNode)tool.Name)]),
                ["runtimeToolListComplete"] = resource.Paging?.IsComplete,
                ["drifts"] = new JsonArray([.. resource.Drifts.Select(drift => (JsonNode)new JsonObject
                {
                    ["kind"] = drift.Kind.ToString(),
                    ["subject"] = drift.Subject,
                    ["declared"] = drift.Declared,
                    ["runtime"] = drift.Runtime,
                    ["evidence"] = new JsonObject
                    {
                        ["registry"] = drift.Evidence.Registry,
                        ["endpoint"] = drift.Evidence.Endpoint,
                        ["probedRevision"] = drift.Evidence.ProbedRevision,
                    },
                })]),
            })]),
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

    private async Task WriteAsync(
        DiscoveryVerifyRequest request,
        DiscoveryVerificationReport report,
        CancellationToken cancellationToken)
    {
        var content = request.Format switch
        {
            OutputFormat.Json => ToJson(report),
            OutputFormat.Sarif => Sarif(report),
            _ => ToText(request, report),
        };

        if (request.OutputPath is not { Length: > 0 } outputPath)
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

    /// <summary>
    /// The findings as SARIF, through the existing serializer.
    /// </summary>
    /// <remarks>
    /// Only the findings. SARIF describes results a scanner produced, and the resource listing underneath them is
    /// a search result — uploading it as static-analysis results would put an annotation on a pull request for
    /// every server a registry happens to hold.
    /// </remarks>
    private string Sarif(DiscoveryVerificationReport report)
    {
        var findings = new Domain.Validation.ValidationReport(
            string.Empty,
            report.Registry,
            report.Diagnostics,
            Domain.Validation.ValidationSummary.FromDiagnostics(report.Diagnostics));

        return _serializers
            .Single(candidate =>
                string.Equals(candidate.Format, OutputFormat.Sarif, StringComparison.OrdinalIgnoreCase))
            .Serialize(findings);
    }
}
