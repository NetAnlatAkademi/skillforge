namespace SkillForge.Application.Skills;

/// <summary>
/// The result of splitting a <c>SKILL.md</c> file.
/// </summary>
/// <param name="Yaml">Contents of the frontmatter block, without its delimiters.</param>
/// <param name="Body">Markdown body following the block.</param>
/// <param name="StartLine">One-based line of the opening delimiter.</param>
/// <param name="EndLine">One-based line of the closing delimiter.</param>
/// <param name="BodyStartLine">
/// One-based line of <see cref="Body"/>'s first line within the file, so a finding about the body can
/// name the line the reader will see in their editor.
/// </param>
/// <param name="TotalLineCount">Total number of lines in the file.</param>
public sealed record FrontmatterSplit(
    string Yaml,
    string Body,
    int StartLine,
    int EndLine,
    int BodyStartLine,
    int TotalLineCount);
