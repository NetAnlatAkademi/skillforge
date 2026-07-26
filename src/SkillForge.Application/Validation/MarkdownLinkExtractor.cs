using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// Finds the local file references in a Markdown body.
/// </summary>
/// <remarks>
/// Only inline links and images are considered, and only those pointing at a local path: external URLs,
/// mail links and pure anchors are somebody else's concern. Links inside fenced code blocks are ignored
/// because they are examples being shown to the reader, not files the skill depends on.
/// </remarks>
public static partial class MarkdownLinkExtractor
{
    /// <summary>
    /// Extracts the local file references from a Markdown body.
    /// </summary>
    /// <param name="body">The Markdown text.</param>
    /// <param name="bodyStartLine">
    /// One-based line of the body's first line within its file, used to report absolute line numbers.
    /// </param>
    /// <returns>The references found, in the order they appear.</returns>
    public static IReadOnlyList<MarkdownLink> Extract(string body, int bodyStartLine)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.Length == 0)
        {
            return [];
        }

        var links = new List<MarkdownLink>();
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var insideFence = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (IsFenceDelimiter(line))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence)
            {
                continue;
            }

            foreach (var match in InlineLinkPattern().Matches(line).Cast<Match>())
            {
                var target = ToLocalPath(match.Groups["target"].Value);
                if (target is not null)
                {
                    links.Add(new MarkdownLink(target, bodyStartLine + index));
                }
            }
        }

        return links;
    }

    /// <summary>
    /// Reduces a link target to a local relative path, or rejects it.
    /// </summary>
    private static string? ToLocalPath(string rawTarget)
    {
        var target = rawTarget.Trim();
        if (target.Length == 0)
        {
            return null;
        }

        // Drop an optional title: [text](path "Title").
        var titleIndex = target.IndexOf('"', StringComparison.Ordinal);
        if (titleIndex > 0)
        {
            target = target[..titleIndex].TrimEnd();
        }

        target = target.Trim('<', '>').Trim();

        if (target.Length == 0
            || target.StartsWith('#')
            || target.StartsWith('/')
            || target.StartsWith('\\')
            || target.Contains("://", StringComparison.Ordinal)
            || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(target))
        {
            return null;
        }

        // Drop an anchor: references/notes.md#section.
        var anchorIndex = target.IndexOf('#', StringComparison.Ordinal);
        if (anchorIndex >= 0)
        {
            target = target[..anchorIndex];
        }

        target = Uri.UnescapeDataString(target).Replace('\\', '/');

        return target.Length == 0 ? null : target;
    }

    private static bool IsFenceDelimiter(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    /// <summary>Matches an inline Markdown link or image, capturing its target.</summary>
    [GeneratedRegex(@"!?\[[^\]]*\]\((?<target>[^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineLinkPattern();
}

/// <summary>
/// A local file reference found in a Markdown body.
/// </summary>
/// <param name="Target">
/// The referenced path as written, with any anchor and title removed, using <c>/</c> separators.
/// </param>
/// <param name="Line">One-based line of the reference within its file.</param>
public sealed record MarkdownLink(string Target, int Line);
