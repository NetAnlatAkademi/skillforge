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

    /// <summary>
    /// Works out how far out of the skill a reference reaches, from the path text alone.
    /// </summary>
    /// <param name="target">Reference as written, using <c>/</c> separators.</param>
    /// <returns>
    /// The scope, and for a sibling reference the name of the sibling directory. No file system is consulted:
    /// whether the target exists is a separate question, and the distinction that matters here — sibling versus
    /// further out — is decided entirely by how many levels the path climbs.
    /// </returns>
    public static ReferenceClassification Classify(string target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var segments = target.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var depth = 0;
        var lowestDepth = 0;
        string? siblingName = null;

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                depth--;
                lowestDepth = Math.Min(lowestDepth, depth);
                continue;
            }

            // The segment that brings a one-level climb back down names the sibling directory.
            if (depth == -1 && siblingName is null)
            {
                siblingName = segment;
            }

            depth++;
        }

        if (lowestDepth >= 0)
        {
            return new ReferenceClassification(ReferenceScope.InsideSkill, Normalise(target), null);
        }

        // One level up and back down is a sibling. Deeper, or ending at the parent itself, is not.
        return lowestDepth == -1 && depth >= 0 && siblingName is not null
            ? new ReferenceClassification(ReferenceScope.SiblingSkill, null, siblingName)
            : new ReferenceClassification(ReferenceScope.OutsideCollection, null, null);
    }
}
