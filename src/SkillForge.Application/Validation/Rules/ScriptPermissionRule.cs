using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports a skill that ships an executable script without declaring that it needs to run anything.
/// </summary>
/// <remarks>
/// Measured on 203 real skills, 7 ship a script — so unlike a rule about URLs, this one is proportionate: it
/// speaks up about roughly three percent of skills, which is what a warning should feel like.
///
/// A script is the difference between a skill that tells an agent what to do and one that hands it something to
/// execute. Saying so in <c>skillforge.yaml</c> is what lets a reader decide before installing, rather than after.
/// </remarks>
public sealed class ScriptPermissionRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ScriptWithoutDeclaredPermission;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (skill.Configuration.DeclaresShellPermission)
        {
            return RuleResult.None();
        }

        var scripts = skill.Resources
            .Where(resource => resource.Kind == SkillResourceKind.Script)
            .ToArray();

        if (scripts.Length == 0)
        {
            return RuleResult.None();
        }

        var names = string.Join(", ", scripts.Select(script => script.RelativePath));

        // The place to *fix* this is skillforge.yaml, but that is not the same as the place to *point at*. Most
        // skills have no skillforge.yaml, and a finding whose location is a file that does not exist sends a reader
        // to an empty path and makes a SARIF consumer annotate something outside the repository. So the location is
        // the configuration file when there is one and SKILL.md when there is not — matching SF1009 and SF1010,
        // which report the same shape of problem — while the suggestion still names the file to create.
        var (file, line) = skill.Configuration.Exists
            ? (SkillDefinition.ConfigurationFileName, (int?)null)
            : (SkillDefinition.SkillFileName, 1);

        return RuleResult.One(Diagnostic.Warning(
            Code,
            $"The skill ships {(scripts.Length == 1 ? "a script" : $"{scripts.Length} scripts")} "
                + $"({names}) but declares no shell permission.",
            file,
            line,
            "List what the skill needs to run under 'permissions.shell.allowed' in "
                + "skillforge.yaml, so somebody deciding whether to install it can see it beforehand."));
    }
}
