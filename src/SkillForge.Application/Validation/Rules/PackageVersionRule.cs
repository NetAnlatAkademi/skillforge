using System.Text.RegularExpressions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Checks that the declared version is a valid version string.
/// </summary>
/// <remarks>
/// The version ends up in a package file name and in the manifest, so consumers need to be able to
/// compare two of them. Semantic versioning is the only scheme that makes that meaningful. A version is
/// optional in this release; only a malformed one is an error.
/// </remarks>
public sealed partial class PackageVersionRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.PackageVersionInvalid;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var version = skill.Frontmatter.Version;
        return version is null || SemanticVersionPattern().IsMatch(version)
            ? RuleResult.None()
            : RuleResult.One(Diagnostic.Error(
                Code,
                $"The version '{version}' is not a valid semantic version.",
                SkillDefinition.SkillFileName,
                skill.Frontmatter.StartLine,
                "Use MAJOR.MINOR.PATCH, for example '1.0.0'."));
    }

    /// <summary>The official semantic versioning pattern, anchored.</summary>
    [GeneratedRegex(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-((0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(\.(0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(\+([0-9a-zA-Z-]+(\.[0-9a-zA-Z-]+)*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();
}
