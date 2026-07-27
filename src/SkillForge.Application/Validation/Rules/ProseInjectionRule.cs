using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Shared behaviour for the `SF4xxx` rules: match a set of patterns against a body's prose and report each
/// pattern at most once.
/// </summary>
/// <remarks>
/// A base class rather than two copies, because the interesting part of these rules is the pattern set and the
/// reading strategy is identical. Everything the derived rules choose — the code, the patterns, the noun the
/// message uses — is abstract; nothing about how the scan works is.
///
/// Reported once per pattern, at the first line it appears on. A body that repeats a phrase has one problem, and
/// a rule that reports it five times just teaches people to stop reading warnings.
/// </remarks>
public abstract class ProseInjectionRule : ISkillValidationRule
{
    /// <inheritdoc />
    public abstract string Code { get; }

    /// <summary>The patterns this rule looks for.</summary>
    protected abstract IReadOnlyList<RiskPattern> Patterns { get; }

    /// <summary>
    /// How the message names what was found, as a sentence completing "The body contains …".
    /// </summary>
    protected abstract string Subject { get; }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        if (string.IsNullOrWhiteSpace(skill.Body))
        {
            return RuleResult.None();
        }

        // Prose, not raw text. A detection pattern shown in a code block is an example, not an instruction —
        // which is exactly the mistake SF3002 made before it was measured.
        var prose = MarkdownProse.Extract(skill.Body, skill.BodyStartLine);
        if (prose.Count == 0)
        {
            return RuleResult.None();
        }

        var diagnostics = new List<Diagnostic>();

        foreach (var pattern in Patterns)
        {
            var line = FirstMatchingLine(prose, pattern);
            if (line is null)
            {
                continue;
            }

            diagnostics.Add(Diagnostic.Warning(
                Code,
                $"The body contains {pattern.Name}.",
                SkillDefinition.SkillFileName,
                line.Value,
                $"{Subject} {pattern.Why}. SkillForge is pointing this out, not calling the skill malicious."));
        }

        return ValueTask.FromResult<IReadOnlyList<Diagnostic>>(diagnostics);
    }

    /// <summary>
    /// Finds the first prose line a pattern matches, or <see langword="null"/> if none does.
    /// </summary>
    private static int? FirstMatchingLine(IReadOnlyList<ProseLine> prose, RiskPattern pattern)
    {
        foreach (var line in prose)
        {
            if (pattern.Pattern.IsMatch(line.Text))
            {
                return line.Line;
            }
        }

        return null;
    }
}
