namespace SkillForge.Domain.Mcp;

/// <summary>
/// How a probe of one MCP server turned out.
/// </summary>
/// <remarks>
/// Four outcomes rather than a boolean, because they call for different actions and must not look alike: a server that
/// answers an older dialect is fine and interesting, while one that cannot be reached says nothing about itself at all.
/// </remarks>
public enum McpProbeStatus
{
    /// <summary>Not asked. A stdio server is never executed, and nothing is probed without <c>--probe-mcp</c>.</summary>
    NotProbed = 0,

    /// <summary>Answered <c>server/discover</c>.</summary>
    Answered,

    /// <summary>
    /// Reachable, but does not implement <c>server/discover</c> — which <c>2026-07-28</c> made mandatory, so it is
    /// almost certainly a handshake-based revision (<c>2025-11-25</c> or earlier). Not a fault.
    /// </summary>
    NoDiscovery,

    /// <summary>Could not be reached, or answered in a shape no adapter understands.</summary>
    Unreachable,
}
