namespace SkillForge.Application.Skills;

/// <summary>
/// Produces the contents of a new skill's files.
/// </summary>
/// <remarks>
/// Pure text generation, so the output can be asserted without touching disk. The generated skill must
/// pass <c>validate</c> with no findings — that is what makes <c>init</c> a useful starting point rather
/// than a first thing to fix, and there is a test that holds it to that.
/// </remarks>
public static class SkillTemplate
{
    /// <summary>Directories every new skill gets.</summary>
    public static IReadOnlyList<string> Directories { get; } =
        ["references", "scripts", "assets", "evals"];

    /// <summary>Builds the <c>SKILL.md</c> contents.</summary>
    /// <param name="options">What to put in it.</param>
    /// <returns>The file contents.</returns>
    public static string CreateSkillFile(SkillTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var description = options.Description is { Length: > 0 } given
            ? given
            : $"Use this skill when working on {options.Name}. Replace this description with the "
                + "situation that should trigger it.";

        var author = options.Author is { Length: > 0 }
            ? $"\n  author: {options.Author}"
            : string.Empty;

        var title = ToTitle(options.Name);

        return $"""
            ---
            name: {options.Name}
            description: {description}
            license: {options.License}
            compatibility:
              - claude-code
              - codex
            metadata:{author}
              version: {options.Version}
            ---

            # {title}

            Describe what this skill does. Keep this file short: an agent reads it in full every time the
            skill activates, so put detail in `references/` and link to it.

            ## When to use this

            - Name the situation that should trigger the skill.
            - Name the situations where it should not.

            ## Workflow

            1. Read the request.
            2. Do the smallest useful thing.
            3. Report what changed.

            """;
    }

    /// <summary>Builds the optional <c>skillforge.yaml</c> contents.</summary>
    /// <param name="options">What to put in it.</param>
    /// <returns>The file contents.</returns>
    public static string CreateConfigurationFile(SkillTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return $"""
            schemaVersion: 1

            package:
              version: {options.Version}
              publisher: local

            compatibility:
              agents:
                - claude-code
                - codex

            # Declare only what the skill actually needs. An empty list means "nothing".
            permissions:
              filesystem:
                read: []
                write: []
              shell:
                allowed: []
              network:
                allowed: false
              secrets: []

            validation:
              strict: false

            packageOptions:
              include:
                - "SKILL.md"
                - "references/**"
                - "scripts/**"
                - "assets/**"
                - "evals/**"
              exclude:
                - ".git/**"
                - "bin/**"
                - "obj/**"
                - ".DS_Store"

            """;
    }

    /// <summary>Turns <c>dotnet-api-review</c> into <c>Dotnet Api Review</c> for the heading.</summary>
    private static string ToTitle(string name) =>
        string.Join(' ', name.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
