using System.Globalization;
using System.Text.RegularExpressions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the skill name is a usable identifier.
/// </summary>
/// <remarks>
/// A name ends up in package file names, directory names and command line arguments across Windows,
/// Linux and macOS. Restricting it to lowercase letters, digits and single hyphens keeps it unambiguous
/// on a case-insensitive file system and safe to type without quoting.
/// </remarks>
public sealed partial class NameFormatRule : ISkillValidationRule
{
    private const int MinimumLength = 2;
    private const int MaximumLength = 64;

    /// <inheritdoc />
    public string Code => DiagnosticCodes.NameInvalid;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);

        // A missing name belongs to SF0004. Reporting it here as well would be noise.
        if (string.IsNullOrWhiteSpace(skill.Name))
        {
            return ValueTask.FromResult<IReadOnlyList<Diagnostic>>([]);
        }

        var reason = DescribeProblem(skill.Name);
        IReadOnlyList<Diagnostic> diagnostics = reason is null
            ? []
            :
            [
                Diagnostic.Error(
                    Code,
                    $"The skill name '{skill.Name}' is not valid: {reason}",
                    SkillDefinition.SkillFileName,
                    skill.Frontmatter.StartLine,
                    "Use lowercase letters, digits and single hyphens, starting with a letter — "
                        + "for example 'dotnet-api-review'."),
            ];

        return ValueTask.FromResult(diagnostics);
    }

    private static string? DescribeProblem(string name)
    {
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

        return ValidNamePattern().IsMatch(name)
            ? null
            : "it may contain only lowercase letters, digits and single hyphens, "
                + "and must start with a letter.";
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidNamePattern();
}
