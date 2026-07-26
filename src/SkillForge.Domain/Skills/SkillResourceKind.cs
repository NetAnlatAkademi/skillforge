namespace SkillForge.Domain.Skills;

/// <summary>
/// Coarse classification of a file inside a skill directory, inferred from its extension.
/// </summary>
/// <remarks>
/// The classification drives informational diagnostics and the <c>inspect</c> summary. It describes
/// what a file looks like, not what it is permitted to do.
/// </remarks>
public enum SkillResourceKind
{
    /// <summary>Extension not recognised as any of the other kinds.</summary>
    Other = 0,

    /// <summary>The skill's own <c>SKILL.md</c> entry point.</summary>
    SkillDocument = 1,

    /// <summary>Markdown documentation, typically under <c>references/</c>.</summary>
    Markdown = 2,

    /// <summary>An executable script, for example <c>.ps1</c>, <c>.sh</c> or <c>.py</c>.</summary>
    Script = 3,

    /// <summary>Structured data such as JSON, YAML, CSV or XML.</summary>
    Data = 4,

    /// <summary>A binary file such as an image, archive or compiled assembly.</summary>
    Binary = 5,
}
