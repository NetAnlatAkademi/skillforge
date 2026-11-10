namespace SkillForge.Domain.Mcp;

/// <summary>
/// One protocol extension a server declared under <c>capabilities.extensions</c>.
/// </summary>
/// <remarks>
/// Only the settings a server set to <see langword="true"/> are kept. Every extension setting the specification has
/// defined so far is a boolean that defaults to <see langword="false"/>, so "absent" and "false" mean the same thing
/// and one list of the enabled ones is the whole of what a client can act on.
/// </remarks>
/// <param name="Identifier">The extension identifier, for example <c>io.modelcontextprotocol/skills</c>.</param>
/// <param name="EnabledSettings">Settings the server set to <see langword="true"/>, ordered.</param>
public sealed record McpExtensionDeclaration(string Identifier, IReadOnlyList<string> EnabledSettings)
{
    /// <summary>Whether the server turned a setting on.</summary>
    /// <param name="setting">The setting name, compared exactly.</param>
    /// <returns><see langword="true"/> when the server set it to <see langword="true"/>.</returns>
    public bool Enables(string setting) => EnabledSettings.Contains(setting, StringComparer.Ordinal);
}
