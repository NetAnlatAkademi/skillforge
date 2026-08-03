using SkillForge.Domain.Diffing;

namespace SkillForge.Domain.Mcp;

/// <summary>
/// How one MCP configuration differs from another in what it would connect to.
/// </summary>
/// <remarks>
/// The same question <c>diff</c> asks about a skill, asked about a configuration: not which bytes changed, but
/// whether an agent would now talk to something it did not talk to before, or reach the same server a different way.
/// </remarks>
/// <param name="BeforePath">The earlier configuration.</param>
/// <param name="AfterPath">The later configuration.</param>
/// <param name="ServersAdded">Servers only the later configuration declares.</param>
/// <param name="ServersRemoved">Servers only the earlier one declares.</param>
/// <param name="Changed">Servers both declare, differently.</param>
public sealed record McpConfigurationDiff(
    string BeforePath,
    string AfterPath,
    IReadOnlyList<string> ServersAdded,
    IReadOnlyList<string> ServersRemoved,
    IReadOnlyList<McpServerChange> Changed)
{
    /// <summary>Gets a value indicating whether anything changed at all.</summary>
    public bool HasChanges => ServersAdded.Count > 0 || ServersRemoved.Count > 0 || Changed.Count > 0;

    /// <summary>
    /// Gets a value indicating whether an agent would now reach something it did not reach before.
    /// </summary>
    /// <remarks>
    /// A new server, or an existing one pointed somewhere else. Both mean a request could now leave for a
    /// destination nobody reviewed; a removed server or a dropped environment variable cannot.
    /// </remarks>
    public bool ReachGrew =>
        ServersAdded.Count > 0
        || Changed.Any(change => change.Command is not null || change.Transport is not null);
}

/// <summary>One server, declared differently by the two configurations.</summary>
/// <param name="Name">The server's name, which is what matched the two declarations.</param>
/// <param name="Transport">How it is reached, when that changed.</param>
/// <param name="Command">The command or URL, when that changed.</param>
/// <param name="Arguments">Arguments added and removed.</param>
/// <param name="EnvironmentVariableNames">
/// Environment variable **names** added and removed. Values are never read, so they cannot be compared — which is
/// deliberate: a diff that printed a rotated token would have leaked it.
/// </param>
public sealed record McpServerChange(
    string Name,
    SurfaceValueChange? Transport,
    SurfaceValueChange? Command,
    SurfaceSetDiff Arguments,
    SurfaceSetDiff EnvironmentVariableNames);
