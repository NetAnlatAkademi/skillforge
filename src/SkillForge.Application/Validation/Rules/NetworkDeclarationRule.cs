using System.Text.RegularExpressions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports a skill whose contents contradict its own network declaration.
/// </summary>
/// <remarks>
/// Not "this skill contains a URL" — that fires on 60 of 203 real skills and says nothing, which is why
/// <c>inspect</c> reports URLs as an observation (SF2002) instead. What is worth a warning is a skill that
/// declares <c>network.allowed: false</c> and then points at a host anyway: the declaration and the content
/// disagree, and one of them is wrong.
///
/// That means this rule is silent on every skill that ships no <c>skillforge.yaml</c> — which today is all of
/// them. That is the right kind of zero: it fires exactly when someone has made a claim worth checking.
/// </remarks>
public sealed partial class NetworkDeclarationRule : ISkillValidationRule
{
    /// <inheritdoc />
    public string Code => DiagnosticCodes.ExternalUrlPresent;

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (skill.Configuration.NetworkAllowed is not false)
        {
            return RuleResult.None();
        }

        var diagnostics = new List<Diagnostic>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = skill.Body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var match in ExternalUrlPattern().Matches(lines[index]).Cast<Match>())
            {
                var url = match.Value.TrimEnd('.', ',', ')', '"', '\'');
                var host = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Host : url;

                if (!seen.Add(host))
                {
                    continue;
                }

                diagnostics.Add(Diagnostic.Warning(
                    Code,
                    $"The skill points at '{host}' but declares 'network.allowed: false'.",
                    SkillDefinition.SkillFileName,
                    skill.BodyStartLine + index,
                    "Either allow the network in skillforge.yaml, or remove the reference. A declaration that "
                        + "contradicts the content misleads whoever reads it to decide whether to install this."));
            }
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }

    [GeneratedRegex(@"https?://[^\s)\]<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalUrlPattern();
}
