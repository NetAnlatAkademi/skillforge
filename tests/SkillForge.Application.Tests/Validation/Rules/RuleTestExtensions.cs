using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Validation.Rules;

/// <summary>Runs a rule with the ceremony a test does not need to spell out.</summary>
internal static class RuleTestExtensions
{
    internal static async Task<IReadOnlyList<Diagnostic>> Run(
        this ISkillValidationRule rule,
        SkillDefinition skill) =>
        await rule.ValidateAsync(skill, CancellationToken.None);
}
