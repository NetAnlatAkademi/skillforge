using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation;

/// <summary>
/// Drops the diagnostics a user has decided not to hear about.
/// </summary>
/// <remarks>
/// Suppression is user policy, not a property of the skill, so it happens after the rules have run rather than by
/// asking the rules to stay quiet. Two consequences are deliberate:
///
/// Any code can be suppressed, including errors. Refusing to suppress errors sounds safer but is not our call —
/// a repository that has decided SF0007 does not apply to it has a legitimate reason we cannot see from here.
///
/// The count is always kept and always reported. A report that quietly omitted findings would be lying about
/// what was checked, and the number is what tells a reader to go look at the configuration.
/// </remarks>
public static class DiagnosticSuppression
{
    /// <summary>
    /// Removes suppressed diagnostics, counting what was removed.
    /// </summary>
    /// <param name="diagnostics">Diagnostics to filter.</param>
    /// <param name="suppressedCodes">Codes to drop, compared case-insensitively.</param>
    /// <returns>The kept diagnostics and how many were dropped.</returns>
    public static SuppressionResult Apply(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyCollection<string> suppressedCodes)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(suppressedCodes);

        if (suppressedCodes.Count == 0)
        {
            return new SuppressionResult(diagnostics, 0);
        }

        var suppressed = suppressedCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var kept = diagnostics
            .Where(diagnostic => !suppressed.Contains(diagnostic.Code))
            .ToArray();

        return new SuppressionResult(kept, diagnostics.Count - kept.Length);
    }
}
