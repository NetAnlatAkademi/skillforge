using System.Globalization;
using System.Text;
using SkillForge.Domain.Graph;

namespace SkillForge.Cli.Commands;

/// <summary>
/// Renders an asset graph as a Mermaid <c>graph LR</c> diagram.
/// </summary>
/// <remarks>
/// Mermaid because it renders in a pull request without a toolchain: GitHub, GitLab and most Markdown viewers draw
/// a fenced <c>mermaid</c> block in place. A reviewer looking at a change to <c>.mcp.json</c> can see what it
/// rewired without installing anything.
///
/// Three properties this file exists to guarantee:
///
/// **Deterministic identifiers.** A Mermaid node id may not contain the characters that appear in real server names,
/// hosts and paths, so each graph id is transliterated. The transliteration is a pure function of the graph id — no
/// counters, no dictionary order — so two runs over unchanged input produce byte-identical diagrams. Where two
/// different graph ids would transliterate to the same identifier, a short hash of the original keeps them apart.
///
/// **Safe labels.** A label is quoted and every character Mermaid would read as syntax is replaced, because a label
/// comes from a filesystem and a filesystem is not a friendly source of text.
///
/// **Names, never values.** A credential node's label is the environment variable's or header's name, which is the
/// only thing the model ever held.
/// </remarks>
internal static class MermaidGraphWriter
{
    /// <summary>Renders the diagram.</summary>
    /// <param name="graph">The graph to draw.</param>
    /// <returns>A Mermaid document, ready to paste into a fenced <c>mermaid</c> block.</returns>
    internal static string Write(AssetGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var builder = new StringBuilder();

        builder.AppendLine("graph LR");

        if (graph.Nodes.Count == 0)
        {
            // A comment rather than nothing: an empty diagram renders as an error in some viewers, and "there was
            // nothing to draw" is a different message from "the diagram is broken".
            builder.AppendLine("  %% no nodes were found under the scanned directory");
            return builder.ToString();
        }

        foreach (var node in graph.Nodes)
        {
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  {Identifier(node.Id)}{Shape(node.Type, Label(node.Name))}");
        }

        if (graph.Edges.Count > 0)
        {
            builder.AppendLine();
        }

        foreach (var edge in graph.Edges)
        {
            // A dotted arrow for an inferred edge and a solid one for a declared one. The confidence is in the
            // label as well: a diagram is read at a glance and printed in black and white.
            var arrow = edge.Confidence == GraphConfidence.Declared ? "-->" : "-.->";

            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  {Identifier(edge.SourceId)} {arrow}|{Label(EdgeLabel(edge))}| {Identifier(edge.TargetId)}");
        }

        return builder.ToString();
    }

    private static string EdgeLabel(GraphEdge edge) =>
        edge.Confidence == GraphConfidence.Declared
            ? edge.Relation.ToString()
            : $"{edge.Relation} (inferred)";

    /// <summary>
    /// The node's box, shaped by type so that the kinds are distinguishable without reading every label.
    /// </summary>
    /// <remarks>
    /// A credential source is a stadium and a host is a rounded box, which are the two shapes a reader is most
    /// likely to be looking for: "what secrets does this reach, and where does it send them".
    /// </remarks>
    private static string Shape(GraphNodeType type, string label) => type switch
    {
        GraphNodeType.ExternalHost => $"({label})",
        GraphNodeType.CredentialSource => $"([{label}])",
        GraphNodeType.Identity => $"{{{{{label}}}}}",
        GraphNodeType.Script or GraphNodeType.Hook => $"[/{label}/]",
        GraphNodeType.Plugin => $"[[{label}]]",
        _ => $"[{label}]",
    };

    /// <summary>
    /// A Mermaid-safe identifier for a graph id.
    /// </summary>
    /// <remarks>
    /// Every character outside <c>a-z</c>, <c>0-9</c> and <c>_</c> becomes an underscore, which collapses
    /// <c>host:api.example.com</c> and <c>host:api-example-com</c> onto the same string. A four-character hash of
    /// the original id is appended whenever anything was replaced, which separates them again without a counter —
    /// counters depend on iteration order, and iteration order is what determinism is about.
    /// </remarks>
    private static string Identifier(string id)
    {
        var builder = new StringBuilder(id.Length + 5);
        var replaced = false;

        foreach (var character in id)
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append('_');
            replaced = true;
        }

        // Mermaid identifiers must not start with a digit.
        if (builder.Length == 0 || char.IsAsciiDigit(builder[0]))
        {
            builder.Insert(0, 'n');
        }

        return replaced ? $"{builder}_{ShortHash(id)}" : builder.ToString();
    }

    /// <summary>
    /// Four hexadecimal characters of a stable, non-cryptographic hash of the text.
    /// </summary>
    /// <remarks>
    /// Written out rather than taken from <see cref="string.GetHashCode()"/>, which is randomised per process and
    /// would give a different diagram on every run. FNV-1a, because it is four lines and this is a label suffix
    /// rather than a security decision.
    /// </remarks>
    private static string ShortHash(string text)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var character in text)
        {
            hash = (hash ^ character) * prime;
        }

        return (hash & 0xFFFF).ToString("x4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A quoted label with everything Mermaid would read as syntax removed.
    /// </summary>
    /// <remarks>
    /// Quoting alone is not enough: a quotation mark inside a quoted label ends it, and a <c>#</c> starts an HTML
    /// entity. Both are replaced rather than escaped, because the label is a name in a diagram and a name that
    /// needs escaping is a name that will be misread by one viewer or another.
    /// </remarks>
    private static string Label(string name) =>
        '"' + name
            .Replace("\"", "'", StringComparison.Ordinal)
            .Replace("#", "_", StringComparison.Ordinal)
            .Replace("|", "/", StringComparison.Ordinal)
            .Replace("[", "(", StringComparison.Ordinal)
            .Replace("]", ")", StringComparison.Ordinal)
            .Replace("{", "(", StringComparison.Ordinal)
            .Replace("}", ")", StringComparison.Ordinal)
            .Replace("<", "(", StringComparison.Ordinal)
            .Replace(">", ")", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            + '"';
}
