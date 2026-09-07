using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Discovery;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Discovery;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge discover</c> does.
/// </summary>
/// <remarks>
/// It reads a registry and prints what the registry said. **Every line of that output is a claim somebody else
/// made**, and the report says so at the top and the bottom rather than leaving it to be inferred — because a
/// clean-looking table of publishers and capabilities is exactly the thing a reader will mistake for verification.
///
/// Nothing here is installed, connected to or executed. Asking a listed server whether it actually has the tools
/// the registry says it has is <c>discovery verify</c>, which is a separate command precisely so that reading a
/// registry and touching a server are separate decisions.
/// </remarks>
internal sealed class DiscoverCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IReadOnlyList<IRemoteResourceDiscoveryAdapter> _adapters;
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the runner.</summary>
    /// <param name="adapters">One adapter per kind of registry.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    public DiscoverCommandRunner(
        IEnumerable<IRemoteResourceDiscoveryAdapter> adapters,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _adapters = [.. adapters];
        _fileSystem = fileSystem;
    }

    /// <summary>Searches one registry.</summary>
    /// <param name="request">What to search for, where, and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/> when the registry was read, <see cref="ExitCodes.InvalidUsage"/> for a
    /// registry that is not a URL or a kind nothing implements, and <see cref="ExitCodes.ValidationFailed"/> when
    /// the registry could not be searched.
    /// </returns>
    internal async Task<int> RunAsync(DiscoverRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Not `Required` on the option, so that this message is the one a user sees. System.CommandLine's own
        // "Option '--registry' is required" names the option; this names the decision, which is the part somebody
        // reaching for a default needs to read.
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
                .WriteLineAsync(
                    $"Usage error:\n'{request.Registry}' is not an http or https registry URL.")
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

        var result = await adapter
            .SearchAsync(
                new RemoteDiscoveryRequest(registry, request.Query, request.Type, request.Limits),
                cancellationToken)
            .ConfigureAwait(false);

        var content = string.Equals(request.Format, OutputFormat.Json, StringComparison.OrdinalIgnoreCase)
            ? ToJson(request, result)
            : ToText(request, result);

        await WriteAsync(request.OutputPath, content, cancellationToken).ConfigureAwait(false);

        // A registry that could not be searched fails the run. "No results" and "no answer" are different facts,
        // and only one of them is a reason to carry on.
        return result.Diagnostics.Any(finding => finding.Severity >= DiagnosticSeverity.Warning)
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    private static string ToText(DiscoverRequest request, RemoteDiscoveryResult result)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Discover");
        builder.AppendLine();
        builder.AppendLine($"Registry:  {result.Registry}");
        builder.AppendLine($"Adapter:   {result.Kind}");
        builder.AppendLine($"Query:     {(request.Query.Length > 0 ? request.Query : "(everything)")}");
        builder.AppendLine($"Resources: {result.Resources.Count}{(result.Truncated ? " (truncated)" : string.Empty)}");
        builder.AppendLine();
        builder.AppendLine("Everything below was DECLARED BY THE REGISTRY. None of it has been verified.");

        foreach (var resource in result.Resources)
        {
            AppendResource(builder, resource);
        }

        if (result.Resources.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("  (the registry listed nothing this adapter could read as a resource)");
        }

        AppendFindings(builder, result);

        builder.AppendLine();
        builder.AppendLine(
            "Registry membership says where something was listed. It is not evidence that it is safe, trusted, "
                + "or what it claims to be — no registry's, official or otherwise.");
        builder.AppendLine(
            "Nothing here was installed, connected to or executed. Use 'skillforge discovery verify' to ask a "
                + "remote HTTP MCP server what it actually exposes.");

        return builder.ToString();
    }

    private static void AppendResource(StringBuilder builder, DiscoveredResource resource)
    {
        builder.AppendLine();
        builder.AppendLine($"  {resource.Name}");
        builder.AppendLine($"      id:           {resource.Id}");
        builder.AppendLine($"      type:         {resource.Type}");
        builder.AppendLine($"      endpoint:     {resource.Endpoint?.ToString() ?? "(none declared)"}");
        builder.AppendLine($"      publisher:    {resource.Publisher ?? "unknown"} (declared, unverified)");
        builder.AppendLine($"      version:      {resource.Version ?? "unknown"}");
        builder.AppendLine(
            $"      capabilities: {resource.Capabilities.Count} declared"
            + (resource.Capabilities.Count > 0
                ? $" ({Sample(resource.DeclaredCapabilityNames)})"
                : string.Empty));

        if (resource.Metadata.Count > 0)
        {
            builder.AppendLine(
                $"      metadata:     {string.Join(", ", resource.Metadata.Keys)} (preserved, never acted on)");
        }

        builder.AppendLine(
            $"      verifiable:   {(resource.IsRemotelyVerifiable ? "yes — remote HTTP MCP server" : "no")}");
    }

    private static void AppendFindings(StringBuilder builder, RemoteDiscoveryResult result)
    {
        if (result.Diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Findings:");

        foreach (var finding in result.Diagnostics)
        {
            builder.AppendLine($"  {finding.Code} {finding.Message}");
        }
    }

    /// <summary>Names a few of them rather than all: a message is read, a list is scrolled past.</summary>
    private static string Sample(IReadOnlyList<string> values) =>
        values.Count <= 5
            ? string.Join(", ", values)
            : $"{string.Join(", ", values.Take(5))} and {values.Count - 5} more";

    private static string ToJson(DiscoverRequest request, RemoteDiscoveryResult result)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["registry"] = result.Registry.ToString(),
            ["adapter"] = result.Kind,
            ["query"] = request.Query,
            ["truncated"] = result.Truncated,

            // Named in the payload, not only in the prose: a JSON consumer is the likeliest to treat a publisher
            // and a capability list as facts SkillForge established.
            ["declaredByRegistry"] = true,
            ["resources"] = new JsonArray([.. result.Resources.Select(resource => (JsonNode)new JsonObject
            {
                ["id"] = resource.Id,
                ["name"] = resource.Name,
                ["type"] = resource.Type.ToString(),
                ["endpoint"] = resource.Endpoint?.ToString(),
                ["publisher"] = resource.Publisher,
                ["version"] = resource.Version,
                ["sourceRegistry"] = resource.SourceRegistry,
                ["remotelyVerifiable"] = resource.IsRemotelyVerifiable,
                ["capabilities"] = new JsonArray([.. resource.Capabilities.Select(capability =>
                    (JsonNode)new JsonObject
                    {
                        ["name"] = capability.Name,
                        ["kind"] = capability.Kind.ToString(),
                        ["description"] = capability.Description,
                    })]),
                ["metadata"] = Metadata(resource),
            })]),
            ["diagnostics"] = new JsonArray([.. result.Diagnostics.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    private static JsonObject Metadata(DiscoveredResource resource)
    {
        var metadata = new JsonObject();

        foreach (var (key, value) in resource.Metadata)
        {
            metadata[key] = value;
        }

        return metadata;
    }

    private async Task WriteAsync(string? outputPath, string content, CancellationToken cancellationToken)
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
