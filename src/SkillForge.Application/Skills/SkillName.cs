using System.Globalization;
using System.Text.RegularExpressions;

namespace SkillForge.Application.Skills;

/// <summary>
/// The single definition of what a valid skill name looks like.
/// </summary>
/// <remarks>
/// Shared by the SF0006 rule and by <c>init</c>, so a name that <c>init</c> accepts can never be one that
/// <c>validate</c> rejects. A name ends up in package file names, directory names and command line
/// arguments across three operating systems, which is why it is restricted this tightly.
/// </remarks>
public static partial class SkillName
{
    /// <summary>Shortest accepted name.</summary>
    public const int MinimumLength = 2;

    /// <summary>Longest accepted name.</summary>
    public const int MaximumLength = 64;

    /// <summary>
    /// Describes what is wrong with a name.
    /// </summary>
    /// <param name="name">Name to examine.</param>
    /// <returns>
    /// A sentence naming the problem, or <see langword="null"/> when the name is valid. An empty name
    /// returns <see langword="null"/>: whether a name is required is a different question, owned by SF0004.
    /// </returns>
    public static string? DescribeProblem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.Length < MinimumLength)
        {
            return "it must be at least "
                + MinimumLength.ToString(CultureInfo.InvariantCulture)
                + " characters long.";
        }

        if (name.Length > MaximumLength)
        {
            return "it must be at most "
                + MaximumLength.ToString(CultureInfo.InvariantCulture)
                + " characters long.";
        }

        return ValidPattern().IsMatch(name)
            ? null
            : "it may contain only lowercase letters, digits and single hyphens, "
                + "and must start with a letter.";
    }

    /// <summary>Determines whether a name is usable.</summary>
    /// <param name="name">Name to check.</param>
    /// <returns><see langword="true"/> when the name is present and valid.</returns>
    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name) && DescribeProblem(name) is null;

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPattern();
}
