using SkillForge.Domain.Skills;

namespace SkillForge.Application.Skills;

/// <summary>
/// Infers a <see cref="SkillResourceKind"/> from a file name.
/// </summary>
/// <remarks>
/// Extension-based on purpose: the loader classifies thousands of files without opening them, and the
/// classification only feeds informational output. Content sniffing belongs to the inspect phase.
/// </remarks>
public static class SkillResourceClassifier
{
    private static readonly HashSet<string> MarkdownExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown", ".mdx" };

    private static readonly HashSet<string> ScriptExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".ps1", ".psm1", ".sh", ".bash", ".zsh", ".fish",
            ".cmd", ".bat", ".py", ".js", ".mjs", ".cjs", ".ts", ".rb", ".pl", ".lua",
        };

    private static readonly HashSet<string> DataExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".yaml", ".yml", ".toml", ".xml", ".csv", ".tsv", ".ini", ".jsonl",
        };

    private static readonly HashSet<string> BinaryExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".bmp", ".svgz",
            ".pdf", ".zip", ".gz", ".tar", ".7z", ".rar",
            ".dll", ".exe", ".so", ".dylib", ".pdb", ".bin", ".wasm",
            ".mp3", ".mp4", ".mov", ".wav", ".woff", ".woff2", ".ttf", ".otf",
        };

    /// <summary>
    /// Classifies a file by its skill-relative path.
    /// </summary>
    /// <param name="relativePath">Path relative to the skill directory, using <c>/</c> separators.</param>
    /// <returns>The inferred kind.</returns>
    public static SkillResourceKind Classify(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (string.Equals(relativePath, SkillDefinition.SkillFileName, StringComparison.OrdinalIgnoreCase))
        {
            return SkillResourceKind.SkillDocument;
        }

        var extension = Path.GetExtension(relativePath);
        if (extension.Length == 0)
        {
            return SkillResourceKind.Other;
        }

        if (MarkdownExtensions.Contains(extension))
        {
            return SkillResourceKind.Markdown;
        }

        if (ScriptExtensions.Contains(extension))
        {
            return SkillResourceKind.Script;
        }

        if (DataExtensions.Contains(extension))
        {
            return SkillResourceKind.Data;
        }

        return BinaryExtensions.Contains(extension) ? SkillResourceKind.Binary : SkillResourceKind.Other;
    }
}
