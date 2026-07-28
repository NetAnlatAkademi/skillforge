namespace SkillForge.Domain.Migration;

/// <summary>
/// How an MCP server is reached.
/// </summary>
/// <remarks>
/// Only what a configuration file states. <see cref="Unknown"/> is not a failure — a declaration that names
/// neither a command nor a URL is reported as it stands rather than guessed at.
/// </remarks>
public enum McpTransport
{
    /// <summary>The configuration says nothing this can be read from.</summary>
    Unknown = 0,

    /// <summary>A local process, launched by command.</summary>
    Stdio,

    /// <summary>A remote endpoint reached over HTTP, including server-sent events.</summary>
    Http,
}
