using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation;

/// <summary>
/// Removes the <see cref="ValueTask{TResult}"/> boilerplate a rule otherwise repeats around its single
/// finding or its absence of one. It changes no behaviour: <see cref="None"/> and <see cref="One"/> return
/// exactly what a rule would have built by hand.
/// </summary>
internal static class RuleResult
{
    /// <summary>The result for a rule that found nothing to report.</summary>
    public static ValueTask<IReadOnlyList<Diagnostic>> None() =>
        ValueTask.FromResult<IReadOnlyList<Diagnostic>>([]);

    /// <summary>The result for a rule that found exactly one thing to report.</summary>
    /// <param name="diagnostic">The single finding.</param>
    public static ValueTask<IReadOnlyList<Diagnostic>> One(Diagnostic diagnostic) =>
        ValueTask.FromResult<IReadOnlyList<Diagnostic>>([diagnostic]);
}
