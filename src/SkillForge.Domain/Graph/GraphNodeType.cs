namespace SkillForge.Domain.Graph;

/// <summary>
/// What kind of thing a node in the asset graph is.
/// </summary>
/// <remarks>
/// The list is longer than what is detected today, and that is the point. A node type is part of the JSON contract
/// once anything emits it, so the ones that are coming — a workflow, a human approval boundary, the harness a skill
/// runs under, the environment it runs in — are named here rather than bolted on later under a different spelling.
/// **A type with no detector produces no nodes**, which is the honest state of affairs and is visible in the report
/// as an absence rather than as an empty section.
/// </remarks>
public enum GraphNodeType
{
    /// <summary>A directory with a <c>SKILL.md</c> in it.</summary>
    Skill = 0,

    /// <summary>A directory with a plugin manifest in it.</summary>
    Plugin,

    /// <summary>An MCP server some configuration declares.</summary>
    McpServer,

    /// <summary>An instruction file an agent reads — <c>CLAUDE.md</c>, <c>AGENTS.md</c>.</summary>
    InstructionFile,

    /// <summary>A hook configuration that runs something on an agent's behalf.</summary>
    Hook,

    /// <summary>An executable script shipped inside an asset.</summary>
    Script,

    /// <summary>A host something in the tree points at.</summary>
    ExternalHost,

    /// <summary>
    /// A named place a credential is read from — an environment variable, an HTTP header. The **name**; a value is
    /// never read into the graph, and never printed by it.
    /// </summary>
    CredentialSource,

    /// <summary>The identity an MCP server is reached with, as far as names and challenges say.</summary>
    Identity,

    /// <summary>
    /// A multi-step automation an agent takes part in. Not detected yet: nothing in a repository declares one in a
    /// form that could be read without guessing.
    /// </summary>
    Workflow,

    /// <summary>
    /// A point where a human must decide before the next step runs. Not detected yet, and deliberately: inferring
    /// one from prose that says "ask the user first" would put a safety control on a diagram that nothing enforces.
    /// </summary>
    ApprovalBoundary,

    /// <summary>The agent harness an asset runs under. Not detected yet.</summary>
    Harness,

    /// <summary>A scheduled or event-driven trigger that runs an agent unattended. Not detected yet.</summary>
    Automation,

    /// <summary>
    /// Where something executes — local, a container, a microVM, a remote sandbox. Not detected yet, and no
    /// provider-specific detector will be written before the model has been used on something real.
    /// </summary>
    ExecutionEnvironment,
}
