using System.Text;

namespace SkillForge.Application.Validation;

/// <summary>
/// Reduces a skill body to the lines a human reads as prose, dropping the code.
/// </summary>
/// <remarks>
/// This class exists because a measurement said it had to. SF3002 once scanned whole bodies for phrases like
/// "ignore previous instructions" and produced twelve findings across 229 real skills, of which roughly one was
/// real. The false positives were not ambiguous English — they were code being displayed to a reader: a YAML
/// comment reading <c># Ignore other fields</c>, and a security skill's own detection pattern written as the
/// string literal <c>r'ignore (previous|all) instructions'</c>.
///
/// So the `SF4xxx` rules read prose, and prose is defined narrowly: fenced code blocks are dropped, and inline
/// code spans are removed from the lines that survive. Both of those are what the measured false positives
/// actually were. Indented blocks are **not** dropped, and no other construct is filtered, because nothing
/// measured justified it — guessing at more exclusions would trade a known false-positive class for an unknown
/// false-negative one.
///
/// What comes out is not quotable text. Code spans are replaced by a space, so a rule matching here should
/// report a line number and a description of what it recognised, never an excerpt.
/// </remarks>
public static class MarkdownProse
{
    private const char Backtick = '`';

    /// <summary>
    /// Extracts the prose lines from a Markdown body.
    /// </summary>
    /// <param name="body">The Markdown text.</param>
    /// <param name="bodyStartLine">
    /// One-based line of the body's first line within its file, so the results carry absolute line numbers.
    /// </param>
    /// <returns>
    /// The non-blank prose lines, in order. Blank lines are dropped: no rule has anything to say about them,
    /// and keeping them would only make callers filter.
    /// </returns>
    public static IReadOnlyList<ProseLine> Extract(string body, int bodyStartLine)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.Length == 0)
        {
            return [];
        }

        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var prose = new List<ProseLine>(lines.Length);
        var insideFence = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (IsFenceDelimiter(line))
            {
                // An unclosed fence therefore swallows the remainder of the body, which is the safe direction:
                // malformed Markdown should not hand a rule a pile of code to misread as prose.
                insideFence = !insideFence;
                continue;
            }

            if (insideFence)
            {
                continue;
            }

            var text = RemoveCodeSpans(line).Trim();
            if (text.Length > 0)
            {
                prose.Add(new ProseLine(text, bodyStartLine + index));
            }
        }

        return prose;
    }

    /// <summary>
    /// Replaces every paired backtick span in a line with a single space.
    /// </summary>
    /// <remarks>
    /// A space rather than nothing, so that removing a span cannot join two words into a phrase that was never
    /// written. An unpaired backtick is left alone: deleting to end of line would silently discard real prose,
    /// and a lone backtick is a typo, not a code sample.
    /// </remarks>
    private static string RemoveCodeSpans(string line)
    {
        if (!line.Contains(Backtick, StringComparison.Ordinal))
        {
            return line;
        }

        var result = new StringBuilder(line.Length);
        var position = 0;

        while (position < line.Length)
        {
            var open = line.IndexOf(Backtick, position);
            if (open < 0)
            {
                result.Append(line, position, line.Length - position);
                break;
            }

            // A run of backticks opens a span that only a run of the same length closes, which is how Markdown
            // lets ``a ` b`` contain a backtick.
            var runLength = RunLength(line, open);
            var close = IndexOfRun(line, open + runLength, runLength);

            if (close < 0)
            {
                result.Append(line, position, line.Length - position);
                break;
            }

            result.Append(line, position, open - position).Append(' ');
            position = close + runLength;
        }

        return result.ToString();
    }

    private static int RunLength(string line, int start)
    {
        var length = 0;
        while (start + length < line.Length && line[start + length] == Backtick)
        {
            length++;
        }

        return length;
    }

    /// <summary>Finds the next run of exactly <paramref name="runLength"/> backticks at or after an index.</summary>
    private static int IndexOfRun(string line, int from, int runLength)
    {
        for (var index = from; index < line.Length; index++)
        {
            if (line[index] != Backtick)
            {
                continue;
            }

            if (RunLength(line, index) == runLength)
            {
                return index;
            }

            index += RunLength(line, index) - 1;
        }

        return -1;
    }

    private static bool IsFenceDelimiter(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }
}
