namespace SkillForge.Application.Validation;

/// <summary>
/// Guesses which interpreters a skill's scripts need, so SF1006's fix can name them.
/// </summary>
/// <remarks>
/// A guess, and presented as one. The point is not to be authoritative — it is that a reader looking at
/// <c>allowed: [node, bash]</c> can tell in a second whether that is right, whereas a reader looking at an empty
/// list has to work out the answer from four files first. Being wrong in a way that is obvious beats being silent.
///
/// Extension-based, not shebang-based. Reading every script to find its shebang would make a rule that currently
/// touches no files start touching them, for a hint.
/// </remarks>
public static class ScriptInterpreters
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".sh"] = "bash",
        [".bash"] = "bash",
        [".zsh"] = "zsh",
        [".js"] = "node",
        [".cjs"] = "node",
        [".mjs"] = "node",
        [".ts"] = "node",
        [".py"] = "python",
        [".rb"] = "ruby",
        [".ps1"] = "pwsh",
        [".psm1"] = "pwsh",
        [".pl"] = "perl",
        [".php"] = "php",
        [".lua"] = "lua",
    };

    /// <summary>
    /// Names the interpreters the given script paths appear to need.
    /// </summary>
    /// <param name="relativePaths">The scripts a skill ships.</param>
    /// <returns>
    /// The distinct interpreter names, sorted so the same skill always produces the same fix text. Empty when
    /// nothing is recognised, which the caller must handle rather than printing an empty list.
    /// </returns>
    public static IReadOnlyList<string> For(IEnumerable<string> relativePaths)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);

        return [.. relativePaths
            .Select(path => Path.GetExtension(path))
            .Select(extension => ByExtension.TryGetValue(extension, out var interpreter) ? interpreter : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }
}
