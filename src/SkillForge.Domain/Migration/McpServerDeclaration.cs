namespace SkillForge.Domain.Migration;

/// <summary>
/// One MCP server as some provider's configuration declares it.
/// </summary>
/// <remarks>
/// **Environment variable values are deliberately absent from this type.** An MCP declaration is one of the
/// likeliest places in a developer's home directory to hold an API token, and a report that prints one has
/// leaked it into a terminal, a CI log or a file the user then pastes somewhere. The names are enough to answer
/// the question this command exists for — "what would this server need if I moved it?" — so the values are never
/// read into the model in the first place, rather than being read and then filtered on the way out.
/// </remarks>
/// <param name="Name">The name the configuration gives the server.</param>
/// <param name="ProviderId">Identifier of the provider whose configuration declared it.</param>
/// <param name="Transport">How it is reached, as far as the configuration says.</param>
/// <param name="Command">
/// The command launched for a stdio server, or the URL for an HTTP one. <see langword="null"/> when the
/// configuration states neither.
/// </param>
/// <param name="Arguments">Arguments as written, in order. Empty when none are declared.</param>
/// <param name="EnvironmentVariableNames">
/// Names of the environment variables the declaration sets, ordered, values excluded.
/// </param>
/// <param name="SourcePath">The configuration file this was read from.</param>
public sealed record McpServerDeclaration(
    string Name,
    string ProviderId,
    McpTransport Transport,
    string? Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> EnvironmentVariableNames,
    string SourcePath);
