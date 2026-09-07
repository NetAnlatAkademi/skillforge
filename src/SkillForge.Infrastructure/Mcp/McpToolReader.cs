using System.Text.Json.Nodes;
using SkillForge.Domain.Mcp;

namespace SkillForge.Infrastructure.Mcp;

/// <summary>
/// Reduces a <c>tools/list</c> payload to the facts a conformance check needs.
/// </summary>
/// <remarks>
/// Shared by both protocol adapters, because the tool shape did not change between the revisions this tool speaks.
///
/// It extracts and does not judge. Whether a missing <c>inputSchema</c> or an odd <c>x-mcp-header</c> is a finding is
/// decided in the Application layer, which is what lets those rules be tested without any JSON at all.
/// </remarks>
internal static class McpToolReader
{
    private const string HeaderAnnotation = "x-mcp-header";

    /// <summary>
    /// Reads the tools array.
    /// </summary>
    /// <param name="tools">The <c>result.tools</c> node, or <see langword="null"/>.</param>
    /// <returns>One summary per tool that names itself; empty when the node is not an array.</returns>
    internal static IReadOnlyList<McpToolSummary> Read(JsonNode? tools)
    {
        if (tools is not JsonArray array)
        {
            return [];
        }

        return
        [
            .. array
                .OfType<JsonObject>()
                .Select(Summarise)
                .OfType<McpToolSummary>(),
        ];
    }

    /// <summary>
    /// Reads the opaque cursor that asks for the next page.
    /// </summary>
    /// <param name="result">The <c>result</c> node of a <c>tools/list</c> response, or <see langword="null"/>.</param>
    /// <returns>
    /// The cursor, or <see langword="null"/> when the response carries none — which is how the specification says the
    /// last page announces itself. An empty string is treated as absent: a server that sends
    /// <c>"nextCursor": ""</c> has ended the list, and asking for page after page of nothing would be a loop.
    /// </returns>
    internal static string? NextCursor(JsonNode? result) =>
        result?["nextCursor"] is JsonValue value
            && value.TryGetValue<string>(out var cursor)
            && cursor.Length > 0
                ? cursor
                : null;

    /// <summary>
    /// Reduces the pages of a tool list to one list: one entry per name, ordered by name.
    /// </summary>
    /// <param name="pages">Every page's tools, in the order the pages were read.</param>
    /// <returns>The merged list.</returns>
    /// <remarks>
    /// Two decisions, both so that two runs against one server agree.
    ///
    /// **Names are compared with ordinal case sensitivity**, because the specification identifies a tool by its name
    /// and does not say two names differing in case are the same tool. Folding case here would silently merge two
    /// real tools.
    ///
    /// **The first description of a repeated name wins.** A server that lists one name twice has made its own surface
    /// ambiguous — a client cannot know which schema applies — and there is no honest way to pick the "right" one.
    /// Taking the first is arbitrary and stated; taking the last would be arbitrary and unstated.
    ///
    /// **The result is ordered by name, not by the order the server listed them.** A paging server is free to return
    /// its pages in whatever order it likes, and a report that changes between two identical runs cannot be diffed in
    /// a pipeline. What the first response carried is kept as a count on
    /// <see cref="SkillForge.Domain.Mcp.McpToolPaging.FirstPageToolCount"/> rather than as a position in this list.
    /// </remarks>
    internal static IReadOnlyList<McpToolSummary> Merge(IEnumerable<IReadOnlyList<McpToolSummary>> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        return
        [
            .. pages
                .SelectMany(page => page)
                .GroupBy(tool => tool.Name, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(tool => tool.Name, StringComparer.Ordinal),
        ];
    }

    private static McpToolSummary? Summarise(JsonObject tool)
    {
        // A tool without a name cannot be reported about usefully, and the specification identifies tools by name.
        if (tool["name"]?.GetValue<string>() is not { } name)
        {
            return null;
        }

        var schema = tool["inputSchema"];

        return new McpToolSummary(
            name,
            schema is JsonObject,
            (schema as JsonObject)?["$schema"]?.GetValue<string>(),
            [.. HeaderAnnotations(schema as JsonObject)]);
    }

    /// <summary>
    /// Finds every <c>x-mcp-header</c> annotation among the schema's top-level properties.
    /// </summary>
    /// <remarks>
    /// Top level only, deliberately. The specification restricts the annotation to properties *statically reachable*
    /// from the schema root, and walking <c>$ref</c>s and composition keywords to decide reachability is a schema
    /// resolver's job. Reading nested properties without that resolution would report annotations that are not actually
    /// reachable — a false positive about a constraint whose whole consequence is a client silently dropping the tool.
    /// The limit is stated in <c>docs/migration.md</c> rather than left to be discovered.
    /// </remarks>
    private static IEnumerable<McpHeaderAnnotation> HeaderAnnotations(JsonObject? schema)
    {
        if (schema?["properties"] is not JsonObject properties)
        {
            yield break;
        }

        foreach (var property in properties)
        {
            if (property.Value is not JsonObject definition
                || definition[HeaderAnnotation] is not { } annotation)
            {
                continue;
            }

            yield return new McpHeaderAnnotation(
                property.Key,
                Text(annotation) ?? string.Empty,
                Text(definition["type"]));
        }
    }

    /// <summary>
    /// A JSON value as text, or <see langword="null"/> when it is not a string — an <c>x-mcp-header</c> that is a number
    /// or an object is not a header name, and treating it as an empty one lets the rule above report it.
    /// </summary>
    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
