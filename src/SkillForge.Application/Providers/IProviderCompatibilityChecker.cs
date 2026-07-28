using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Providers;

/// <summary>
/// Checks a skill against the providers it claims to work with.
/// </summary>
public interface IProviderCompatibilityChecker
{
    /// <summary>
    /// Checks the skill against the providers it declares, plus any the caller asked about.
    /// </summary>
    /// <param name="skill">The skill to check.</param>
    /// <param name="additionalProviders">
    /// Providers the caller wants checked even though the skill does not declare them — <c>--provider</c>. A
    /// provider already declared is not checked or reported twice.
    /// </param>
    /// <returns>The findings, in the order the providers were considered.</returns>
    IReadOnlyList<Diagnostic> Check(SkillDefinition skill, IReadOnlyList<string> additionalProviders);
}
