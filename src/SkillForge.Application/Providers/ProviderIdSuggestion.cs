namespace SkillForge.Application.Providers;

/// <summary>
/// Works out which known provider identifier an unrecognised one was meant to be.
/// </summary>
/// <remarks>
/// Three shapes of near-miss are worth naming, and nothing beyond them is: a different separator
/// (<c>claude_code</c>), a short form (<c>copilot</c>), and a small misspelling (<c>claude-cod</c>). Anything
/// further away is a guess, and a wrong suggestion costs more than no suggestion — so this returns
/// <see langword="null"/> rather than the least-bad candidate. Ambiguity is treated the same way.
/// </remarks>
internal static class ProviderIdSuggestion
{
    /// <summary>Shortest short form worth matching by containment; below this, "code" would match anything.</summary>
    private const int MinimumShortFormLength = 4;

    /// <summary>How many single-character edits still counts as a misspelling rather than a different word.</summary>
    private const int MaximumEditDistance = 2;

    /// <summary>
    /// Finds the candidate the given identifier most likely meant.
    /// </summary>
    /// <param name="id">The unrecognised identifier.</param>
    /// <param name="candidates">Known identifiers.</param>
    /// <returns>The matching candidate, or <see langword="null"/> when none is close enough or several are.</returns>
    internal static string? Closest(string id, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(candidates);

        var normalized = Normalize(id);

        if (normalized.Length == 0)
        {
            return null;
        }

        var known = candidates.Select(candidate => (Id: candidate, Normalized: Normalize(candidate))).ToArray();

        // A separator or casing difference is the same identifier written differently, so it wins outright.
        var sameWord = OnlyOne(known
            .Where(candidate => string.Equals(candidate.Normalized, normalized, StringComparison.Ordinal))
            .Select(candidate => candidate.Id));

        if (sameWord is not null)
        {
            return sameWord;
        }

        if (normalized.Length >= MinimumShortFormLength)
        {
            var shortForm = OnlyOne(known
                .Where(candidate => candidate.Normalized.Contains(normalized, StringComparison.Ordinal))
                .Select(candidate => candidate.Id));

            if (shortForm is not null)
            {
                return shortForm;
            }
        }

        return ClosestByEditDistance(normalized, known);
    }

    private static string? ClosestByEditDistance(
        string normalized,
        IReadOnlyList<(string Id, string Normalized)> known)
    {
        var withinReach = known
            .Select(candidate => (candidate.Id, Distance: EditDistance(normalized, candidate.Normalized)))
            .Where(candidate => candidate.Distance <= MaximumEditDistance)
            .ToArray();

        if (withinReach.Length == 0)
        {
            return null;
        }

        var nearest = withinReach.Min(candidate => candidate.Distance);

        return OnlyOne(withinReach
            .Where(candidate => candidate.Distance == nearest)
            .Select(candidate => candidate.Id));
    }

    /// <summary>
    /// Returns the one match, or <see langword="null"/> when there is none or more than one. Two equally close
    /// candidates mean SkillForge does not know which was meant, and saying so is the honest answer.
    /// </summary>
    private static string? OnlyOne(IEnumerable<string> matches)
    {
        var found = matches.Take(2).ToArray();

        return found.Length == 1 ? found[0] : null;
    }

    /// <summary>Strips everything a person might vary — case, hyphens, underscores, dots, spaces.</summary>
    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>Levenshtein distance, two rows at a time because the identifiers are short.</summary>
    private static int EditDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1);

                current[column] = Math.Min(
                    Math.Min(previous[column] + 1, current[column - 1] + 1),
                    substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
