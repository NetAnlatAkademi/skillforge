using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain;

/// <summary>
/// Outcome of an operation that can fail in expected ways.
/// </summary>
/// <remarks>
/// Expected failures — a missing file, unparsable YAML — are returned, never thrown. Exceptions are
/// reserved for genuinely unexpected states. A successful result may still carry diagnostics: a skill
/// can load correctly and still be worth warning about.
/// </remarks>
/// <typeparam name="T">Type of the produced value.</typeparam>
/// <param name="IsSuccess">Whether the operation produced a usable value.</param>
/// <param name="Value">The produced value, or <see langword="null"/> when the operation failed.</param>
/// <param name="Diagnostics">Findings collected while performing the operation.</param>
public sealed record OperationResult<T>(
    bool IsSuccess,
    T? Value,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Findings collected on the way, if any.</param>
    /// <returns>A successful result carrying <paramref name="value"/>.</returns>
    public static OperationResult<T> Success(T value, IReadOnlyList<Diagnostic>? diagnostics = null) =>
        new(true, value, diagnostics ?? []);

    /// <summary>Creates a failed result.</summary>
    /// <param name="diagnostics">Findings explaining the failure. Should contain at least one error.</param>
    /// <returns>A failed result with no value.</returns>
    public static OperationResult<T> Failure(IReadOnlyList<Diagnostic> diagnostics) =>
        new(false, default, diagnostics);

    /// <summary>Creates a failed result from a single diagnostic.</summary>
    /// <param name="diagnostic">The finding explaining the failure.</param>
    /// <returns>A failed result with no value.</returns>
    public static OperationResult<T> Failure(Diagnostic diagnostic) => new(false, default, [diagnostic]);
}
