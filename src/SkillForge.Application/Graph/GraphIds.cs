using SkillForge.Domain.Graph;

namespace SkillForge.Application.Graph;

/// <summary>
/// Builds the node identifiers the graph is keyed on, and the relative paths its evidence is written in.
/// </summary>
/// <remarks>
/// One place, because an id has to be identical between the run that produced a graph and the run that is compared
/// against it. A prefix per type keeps two different things with the same name — a skill and the plugin that ships
/// it, a host and an environment variable — from collapsing into one node.
///
/// Ids are not sanitised for any output format here. Mermaid has its own rules about what may appear in an
/// identifier, and encoding them into the model would make the JSON contract depend on a diagram syntax.
/// </remarks>
internal static class GraphIds
{
    /// <summary>The id of a node of the given type with the given name.</summary>
    /// <param name="type">The node's type.</param>
    /// <param name="name">The node's name or path.</param>
    /// <returns>The id.</returns>
    internal static string Of(GraphNodeType type, string name) => $"{Prefix(type)}:{name}";

    /// <summary>
    /// A path relative to the scanned root, with <c>/</c> separators.
    /// </summary>
    /// <param name="root">The scanned root.</param>
    /// <param name="path">An absolute path inside it.</param>
    /// <returns>The relative path, or the input unchanged when it is not under the root.</returns>
    /// <remarks>
    /// Relative, because a build agent's absolute path means nothing to whoever reads the report afterwards, and
    /// because two checkouts of the same repository must produce the same graph. Forward slashes for the same
    /// reason the rest of the tool uses them: a report should not differ between Windows and Linux.
    /// </remarks>
    internal static string Relative(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');

        return relative.StartsWith("../", StringComparison.Ordinal) ? path.Replace('\\', '/') : relative;
    }

    private static string Prefix(GraphNodeType type) => type switch
    {
        GraphNodeType.Skill => "skill",
        GraphNodeType.Plugin => "plugin",
        GraphNodeType.McpServer => "mcp",
        GraphNodeType.InstructionFile => "instruction",
        GraphNodeType.Hook => "hook",
        GraphNodeType.Script => "script",
        GraphNodeType.ExternalHost => "host",
        GraphNodeType.CredentialSource => "credential",
        GraphNodeType.Identity => "identity",
        GraphNodeType.Workflow => "workflow",
        GraphNodeType.ApprovalBoundary => "approval",
        GraphNodeType.Harness => "harness",
        GraphNodeType.Automation => "automation",
        GraphNodeType.ExecutionEnvironment => "environment",

        // Unreachable while the switch covers the enum, and a thrown exception is the right answer if a type is
        // added without one: a node with no prefix would collide with another type's ids silently.
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no id prefix is defined for this node type"),
    };
}
