using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Application.Graph;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Graph;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge graph</c> does.
/// </summary>
/// <remarks>
/// **A graph is not a finding, so this command always exits zero** once it has read the directory. It is
/// descriptive in the same sense as <c>inspect</c>, <c>inventory</c> and <c>provenance</c> (ADR-006): it says what
/// is wired to what and leaves the judging to <c>policy check</c>. A `--fail-on-something` flag would need a rule
/// behind it, and the composition rules this graph makes possible have not been measured on real repositories yet.
///
/// A configuration file that could not be parsed is the one thing that fails the run, for the reason
/// <c>mcp inspect</c> fails on it: a node missing from a graph because a file would not read is worse than a
/// stated gap, and worst of all when it exits zero.
/// </remarks>
internal sealed class GraphCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly GraphBuilder _builder;
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the runner.</summary>
    /// <param name="builder">Reads the directory into a graph.</param>
    /// <param name="fileSystem">Writes machine-readable output when asked.</param>
    public GraphCommandRunner(GraphBuilder builder, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _builder = builder;
        _fileSystem = fileSystem;
    }

    /// <summary>Builds and presents the graph.</summary>
    /// <param name="request">What to read and how to present it.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/>, or <see cref="ExitCodes.ValidationFailed"/> when a configuration file in
    /// the tree could not be read.
    /// </returns>
    internal async Task<int> RunAsync(GraphRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var graph = await _builder.BuildAsync(request.Path, cancellationToken).ConfigureAwait(false);

        var content = request.Format switch
        {
            OutputFormat.Json => ToJson(graph),
            OutputFormat.Mermaid => MermaidGraphWriter.Write(graph),
            _ => ToText(graph),
        };

        await WriteAsync(request.OutputPath, content, cancellationToken).ConfigureAwait(false);

        return graph.Diagnostics.Any(finding =>
            finding.Code == DiagnosticCodes.ProviderConfigurationNotParsable)
                ? ExitCodes.ValidationFailed
                : ExitCodes.Success;
    }

    private static string ToText(AssetGraph graph)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SkillForge Graph");
        builder.AppendLine();
        builder.AppendLine($"Root:  {graph.Root}");
        builder.AppendLine($"Nodes: {graph.Nodes.Count}");
        builder.AppendLine($"Edges: {graph.Edges.Count}");

        AppendNodes(builder, graph);
        AppendEdges(builder, graph);
        AppendFindings(builder, graph);

        builder.AppendLine();
        builder.AppendLine(
            "Every edge names the file it was read from. A dotted 'inferred' edge came from a name matched in "
                + "prose or from the spelling of a variable — exact evidence, and a heuristic conclusion.");
        builder.AppendLine(
            "Credential nodes are environment variable and header names. No value is ever read or printed.");
        builder.AppendLine(
            "A graph describes; it does not judge. Nothing here is a finding, and this command exits 0.");

        return builder.ToString();
    }

    private static void AppendNodes(StringBuilder builder, AssetGraph graph)
    {
        builder.AppendLine();
        builder.AppendLine("Nodes:");

        if (graph.Nodes.Count == 0)
        {
            builder.AppendLine("  (nothing the graph knows about was found)");
            return;
        }

        foreach (var group in graph.Nodes.GroupBy(node => node.Type).OrderBy(group => group.Key))
        {
            builder.AppendLine();
            builder.AppendLine($"  {group.Key} ({group.Count()}):");

            foreach (var node in group)
            {
                builder.AppendLine(
                    $"      {node.Name}"
                    + (node.Source is { Length: > 0 } source ? $"  — {source}" : string.Empty));
            }
        }
    }

    private static void AppendEdges(StringBuilder builder, AssetGraph graph)
    {
        builder.AppendLine();
        builder.AppendLine("Edges:");

        if (graph.Edges.Count == 0)
        {
            builder.AppendLine("  (nothing was found evidence for)");
            return;
        }

        foreach (var edge in graph.Edges)
        {
            var evidence = edge.Evidence.Line is { } line
                ? $"{edge.Evidence.File}:{line}"
                : edge.Evidence.File;

            builder.AppendLine(
                $"  {Name(graph, edge.SourceId)} --{edge.Relation}--> {Name(graph, edge.TargetId)}");
            builder.AppendLine(
                $"      {edge.Confidence.ToString().ToLowerInvariant()} · {edge.Evidence.Kind} · {evidence}");
        }
    }

    private static void AppendFindings(StringBuilder builder, AssetGraph graph)
    {
        if (graph.Diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Could not be read:");

        foreach (var finding in graph.Diagnostics)
        {
            builder.AppendLine($"  {finding.Code} {finding.Message}");
        }
    }

    /// <summary>
    /// A node's name for console output, falling back to the id.
    /// </summary>
    /// <remarks>
    /// The fallback is unreachable while every edge endpoint is a node the collector created, and it is here so
    /// that a future edge pointing at something absent prints an id rather than an empty gap.
    /// </remarks>
    private static string Name(AssetGraph graph, string id) => graph.Node(id)?.Name ?? id;

    private static string ToJson(AssetGraph graph)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = Reporting.SkillForgeTool.ReportSchemaVersion,
            ["root"] = graph.Root,
            ["nodes"] = new JsonArray([.. graph.Nodes.Select(node => (JsonNode)new JsonObject
            {
                ["id"] = node.Id,
                ["type"] = node.Type.ToString(),
                ["name"] = node.Name,
                ["source"] = node.Source,
            })]),
            ["edges"] = new JsonArray([.. graph.Edges.Select(edge => (JsonNode)new JsonObject
            {
                ["source"] = edge.SourceId,
                ["target"] = edge.TargetId,
                ["relation"] = edge.Relation.ToString(),
                ["confidence"] = edge.Confidence.ToString(),
                ["evidence"] = new JsonObject
                {
                    ["file"] = edge.Evidence.File,
                    ["line"] = edge.Evidence.Line,
                    ["kind"] = edge.Evidence.Kind,
                },
            })]),
            ["diagnostics"] = new JsonArray([.. graph.Diagnostics.Select(finding => (JsonNode)new JsonObject
            {
                ["code"] = finding.Code,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["message"] = finding.Message,
                ["filePath"] = finding.FilePath,
            })]),
        };

        return document.ToJsonString(JsonOptions) + Environment.NewLine;
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
