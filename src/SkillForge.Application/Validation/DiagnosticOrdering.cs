using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation;

/// <summary>
/// Puts diagnostics into the order every report uses.
/// </summary>
/// <remarks>
/// Ordering is part of the contract, not a presentation detail: snapshot tests compare output, and a CI
/// log that reshuffles between runs is unreadable. Errors come first because they are what stops the
/// user, then the order is fully determined by code, file and line.
/// </remarks>
public static class DiagnosticOrdering
{
    /// <summary>
    /// Orders diagnostics by descending severity, then by code, file path and line.
    /// </summary>
    /// <param name="diagnostics">Diagnostics to order.</param>
    /// <returns>A new ordered list.</returns>
    public static IReadOnlyList<Diagnostic> Sort(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        // LINQ's ordering is stable, so equally ranked findings keep the order the rules produced them
        // in. That is what makes two runs over unchanged input produce byte-identical reports.
        return diagnostics
            .OrderByDescending(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Line)
            .ToArray();
    }
}
