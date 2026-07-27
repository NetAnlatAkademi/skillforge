namespace SkillForge.Domain.Skills;

/// <summary>
/// A skill as it was found on disk: its frontmatter, its Markdown body and the files around it.
/// </summary>
/// <remarks>
/// This is the loader's output and the input to every validation rule. It records what the skill
/// says about itself without judging it — <see cref="Name"/> and <see cref="Description"/> are empty
/// strings when the frontmatter omits them, which the required-field rules then report.
/// </remarks>
/// <param name="Name">Declared skill name, or an empty string when the field is absent.</param>
/// <param name="Description">Declared description, or an empty string when the field is absent.</param>
/// <param name="DirectoryPath">Normalised absolute path of the skill directory.</param>
/// <param name="SkillFilePath">Normalised absolute path of the <c>SKILL.md</c> file.</param>
/// <param name="Frontmatter">The parsed frontmatter block.</param>
/// <param name="Resources">
/// Every file inside the skill directory, including <c>SKILL.md</c>, ordered by relative path.
/// </param>
/// <param name="Body">The Markdown body: everything after the closing frontmatter delimiter.</param>
/// <param name="BodyStartLine">
/// One-based line of <see cref="Body"/>'s first line within <c>SKILL.md</c>, so findings about the body
/// can name the line the reader sees in their editor.
/// </param>
/// <param name="SkillFileLineCount">Total number of lines in <c>SKILL.md</c>, frontmatter included.</param>
public sealed record SkillDefinition(
    string Name,
    string Description,
    string DirectoryPath,
    string SkillFilePath,
    SkillFrontmatter Frontmatter,
    IReadOnlyList<SkillResource> Resources,
    string Body,
    int BodyStartLine,
    int SkillFileLineCount)
{
    /// <summary>
    /// What the skill's own <c>skillforge.yaml</c> declares, or the defaults when it ships none.
    /// </summary>
    /// <remarks>
    /// Part of the loaded skill because the file is one of the skill's files. Rules that compare what a skill
    /// declares against what it contains — a script with no shell permission, a URL with the network declared off —
    /// need it, and having the loader read it keeps that comparison inside the rules rather than in a command class.
    /// </remarks>
    public Validation.SkillConfiguration Configuration { get; init; } = Validation.SkillConfiguration.Default;

    /// <summary>The conventional file name of a skill's entry point.</summary>
    public const string SkillFileName = "SKILL.md";

    /// <summary>The conventional file name of SkillForge's own optional configuration file.</summary>
    public const string ConfigurationFileName = "skillforge.yaml";
}
