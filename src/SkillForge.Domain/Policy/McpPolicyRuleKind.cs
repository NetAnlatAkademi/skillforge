namespace SkillForge.Domain.Policy;

/// <summary>
/// How an MCP policy rule identifies a server.
/// </summary>
/// <remarks>
/// Three kinds rather than one, because they are not equally trustworthy. A URL and a command say what would
/// actually be connected to or launched; a display name says what a configuration file chose to call it, and a
/// configuration file is the thing under review. Keeping them apart is what lets a match on a name alone be
/// reported as such (<c>SF8105</c>) instead of passing as an identification.
/// </remarks>
public enum McpPolicyRuleKind
{
    /// <summary>The rule names a remote server by URL, possibly with <c>*</c> wildcards.</summary>
    ServerUrl,

    /// <summary>The rule names a local server by the command it launches, and optionally its arguments.</summary>
    ServerCommand,

    /// <summary>The rule names a server by the name its configuration gives it.</summary>
    ServerName,
}
