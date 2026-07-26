namespace SkillForge.Application.Validation;

/// <summary>
/// Collapses a skill-relative path written by hand into the form the file inventory uses.
/// </summary>
/// <remarks>
/// Pure string work: this answers "which file did the author mean?" without touching the disk, which is
/// what lets the reference rules stay unit testable. Whether that file exists is a separate question.
/// </remarks>
public static class SkillRelativePath
{
    /// <summary>
    /// Normalises a reference by removing <c>./</c> and collapsing <c>..</c> segments.
    /// </summary>
    /// <param name="target">Reference as written, using <c>/</c> separators.</param>
    /// <returns>
    /// The collapsed path, or <see langword="null"/> when it walks out of the skill directory.
    /// </returns>
    public static string? Normalise(string target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var segments = target.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resolved = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment != "..")
            {
                resolved.Add(segment);
                continue;
            }

            if (resolved.Count == 0)
            {
                return null; // Walked past the skill directory.
            }

            resolved.RemoveAt(resolved.Count - 1);
        }

        return resolved.Count == 0 ? null : string.Join('/', resolved);
    }
}
