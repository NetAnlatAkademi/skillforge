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
        var body = closingIndex.Value + 1 < lines.Length
            ? string.Join('\n', lines[(closingIndex.Value + 1)..]).TrimStart('\n')
            : string.Empty;

        return new FrontmatterSplit(
            Yaml: yaml,
            Body: body,
            StartLine: openingIndex.Value + 1,
            EndLine: closingIndex.Value + 1,
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

/// <summary>
/// The result of splitting a <c>SKILL.md</c> file.
/// </summary>
/// <param name="Yaml">Contents of the frontmatter block, without its delimiters.</param>
/// <param name="Body">Markdown body following the block.</param>
/// <param name="StartLine">One-based line of the opening delimiter.</param>
/// <param name="EndLine">One-based line of the closing delimiter.</param>
/// <param name="TotalLineCount">Total number of lines in the file.</param>
public sealed record FrontmatterSplit(
    string Yaml,
    string Body,
    int StartLine,
    int EndLine,
    int TotalLineCount);
