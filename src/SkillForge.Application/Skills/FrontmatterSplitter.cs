namespace SkillForge.Application.Skills;

/// <summary>
/// Splits a <c>SKILL.md</c> file into its frontmatter block and its Markdown body.
/// </summary>
/// <remarks>
/// Pure text handling, deliberately independent of any YAML library: the block is delimited by a line
/// containing only <c>---</c> at the very start of the file, and closed by the next line containing only
/// <c>---</c> or <c>...</c>. Both LF and CRLF endings are accepted, and a UTF-8 byte order mark is
/// tolerated.
/// </remarks>
public static class FrontmatterSplitter
{
    private const string ByteOrderMark = "﻿";

    /// <summary>
    /// Attempts to split the contents of a <c>SKILL.md</c> file.
    /// </summary>
    /// <param name="content">Full contents of the file.</param>
    /// <returns>
    /// The split result, or <see langword="null"/> when the file does not open with a frontmatter
    /// delimiter or the block is never closed.
    /// </returns>
    public static FrontmatterSplit? TrySplit(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = SplitLines(content.StartsWith(ByteOrderMark, StringComparison.Ordinal)
            ? content[ByteOrderMark.Length..]
            : content);

        var openingIndex = FindOpeningDelimiter(lines);
        if (openingIndex is null)
        {
            return null;
        }

        var closingIndex = FindClosingDelimiter(lines, openingIndex.Value + 1);
        if (closingIndex is null)
        {
            return null;
        }

        var yaml = string.Join('\n', lines[(openingIndex.Value + 1)..closingIndex.Value]);

        // Blank lines between the block and the first line of prose are skipped, but counted, so that a
        // diagnostic about the body can still name the right line of the file.
        var bodyIndex = closingIndex.Value + 1;
        while (bodyIndex < lines.Length && lines[bodyIndex].Trim().Length == 0)
        {
            bodyIndex++;
        }

        var body = bodyIndex < lines.Length ? string.Join('\n', lines[bodyIndex..]) : string.Empty;

        return new FrontmatterSplit(
            Yaml: yaml,
            Body: body,
            StartLine: openingIndex.Value + 1,
            EndLine: closingIndex.Value + 1,
            BodyStartLine: bodyIndex + 1,
            TotalLineCount: lines.Length);
    }

    /// <summary>
    /// Counts the lines of a text file the same way <see cref="TrySplit"/> does.
    /// </summary>
    /// <param name="content">Text to measure.</param>
    /// <returns>Number of lines.</returns>
    public static int CountLines(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SplitLines(content).Length;
    }

    private static string[] SplitLines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static int? FindOpeningDelimiter(string[] lines)
    {
        // Only leading blank lines may precede the block; anything else means there is no frontmatter.
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            return IsOpeningDelimiter(line) ? index : null;
        }

        return null;
    }

    private static int? FindClosingDelimiter(string[] lines, int startIndex)
    {
        for (var index = startIndex; index < lines.Length; index++)
        {
            if (IsClosingDelimiter(lines[index].TrimEnd()))
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsOpeningDelimiter(string line) => line is "---";

    private static bool IsClosingDelimiter(string line) => line is "---" or "...";
}
