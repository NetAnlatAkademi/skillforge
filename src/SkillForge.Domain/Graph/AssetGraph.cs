using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Graph;

/// <summary>
/// One thing in the graph.
/// </summary>
/// <param name="Id">
/// A stable identifier, built from the node's type and its name or path — <c>skill:deploy-skill</c>,
/// <c>host:prod.company.com</c>. Deterministic across runs and across machines, because a graph whose node ids
/// change between two runs cannot be diffed, and diffing is what a graph is eventually for.
/// </param>
/// <param name="Type">What kind of thing it is.</param>
/// <param name="Name">
/// What to call it in a report. A credential source's name is the environment variable's or header's **name** —
/// never a value, at any point in this type's life.
/// </param>
/// <param name="Source">
/// The file or directory the node was found in, relative to the scanned root, or <see langword="null"/> for a node
/// that is not a place — a host, a credential name, an identity.
/// </param>
public sealed record GraphNode(string Id, GraphNodeType Type, string Name, string? Source);

/// <summary>
/// One claim about two nodes.
/// </summary>
/// <param name="SourceId">Id of the node the relation starts at.</param>
/// <param name="TargetId">Id of the node it points at.</param>
/// <param name="Relation">What the source does to the target.</param>
/// <param name="Confidence">Whether this was declared somewhere or inferred from a name.</param>
/// <param name="Evidence">Which file, and where in it, the claim was read.</param>
public sealed record GraphEdge(
    string SourceId,
    string TargetId,
    GraphRelation Relation,
    GraphConfidence Confidence,
    GraphEvidence Evidence);

/// <summary>
/// What <c>skillforge graph</c> found under one directory.
/// </summary>
/// <remarks>
/// **A graph is not a finding.** It describes what is wired to what and exits zero, exactly as <c>inspect</c>,
/// <c>inventory</c> and <c>provenance</c> do (ADR-006). Nothing here decides that a shape is dangerous — a
/// composition rule of that sort has to be measured on real repositories before it is published, and until then a
/// drawing that implied a verdict would be a verdict nobody could appeal.
///
/// Nodes and edges are both ordered, so two runs over unchanged input produce byte-identical output in every format.
/// </remarks>
/// <param name="Root">Directory that was scanned.</param>
/// <param name="Nodes">Nodes found, ordered by id.</param>
/// <param name="Edges">Edges found, ordered by source, then target, then relation.</param>
/// <param name="Diagnostics">
/// What SkillForge could not read, and why. About its own reading, not about the graph: a configuration file it
/// failed to parse is reported so that a missing node is a stated gap rather than a silent one.
/// </param>
public sealed record AssetGraph(
    string Root,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>A graph over a directory that held nothing the graph knows about.</summary>
    /// <param name="root">The directory that was scanned.</param>
    /// <returns>An empty graph.</returns>
    public static AssetGraph Empty(string root) => new(root, [], [], []);

    /// <summary>Gets how many nodes there are of each type, ordered by type.</summary>
    public IReadOnlyList<(GraphNodeType Type, int Count)> NodeCounts =>
    [
        .. Nodes
            .GroupBy(node => node.Type)
            .Select(group => (Type: group.Key, Count: group.Count()))
            .OrderBy(entry => entry.Type),
    ];

    /// <summary>Gets how many edges there are of each relation, ordered by relation.</summary>
    public IReadOnlyList<(GraphRelation Relation, int Count)> EdgeCounts =>
    [
        .. Edges
            .GroupBy(edge => edge.Relation)
            .Select(group => (Relation: group.Key, Count: group.Count()))
            .OrderBy(entry => entry.Relation),
    ];

    /// <summary>Finds a node by id.</summary>
    /// <param name="id">The id to look for.</param>
    /// <returns>The node, or <see langword="null"/> when the graph has none with that id.</returns>
    public GraphNode? Node(string id) =>
        Nodes.FirstOrDefault(node => string.Equals(node.Id, id, StringComparison.Ordinal));
}
