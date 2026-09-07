namespace SkillForge.Domain.Graph;

/// <summary>
/// What one node does to another.
/// </summary>
/// <remarks>
/// **Only a relation something was found evidence for is ever emitted.** There is no "probably talks to" edge and no
/// transitive closure: if a skill invokes a server and the server reaches a host, the graph says exactly that and
/// leaves the reader to follow the two hops. An edge SkillForge invented would be indistinguishable, on the diagram,
/// from one it read.
/// </remarks>
public enum GraphRelation
{
    /// <summary>The source holds the target — a plugin holding a skill, a skill holding a script.</summary>
    Contains = 0,

    /// <summary>The source's text names the target.</summary>
    References,

    /// <summary>The source calls the target.</summary>
    Invokes,

    /// <summary>The source opens a network connection to the target.</summary>
    ConnectsTo,

    /// <summary>The source reads a credential from the target.</summary>
    ReadsCredential,

    /// <summary>The source authenticates as the target identity.</summary>
    UsesIdentity,

    /// <summary>The source runs the target as a process.</summary>
    Executes,

    /// <summary>The source declares the target's existence — a configuration file and the server in it.</summary>
    Declares,

    /// <summary>The source is distributed by the target.</summary>
    DistributedBy,

    /// <summary>The source takes its updates from the target.</summary>
    UpdatesFrom,

    /// <summary>
    /// The source needs the target's approval before it proceeds. Not emitted yet — see
    /// <see cref="GraphNodeType.ApprovalBoundary"/>.
    /// </summary>
    ApprovedBy,

    /// <summary>
    /// The source executes inside the target. Not emitted yet — see
    /// <see cref="GraphNodeType.ExecutionEnvironment"/>.
    /// </summary>
    RunsWithin,
}
