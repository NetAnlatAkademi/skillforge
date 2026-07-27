using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Validation;

/// <summary>
/// What survived suppression, and how much did not.
/// </summary>
/// <param name="Kept">The diagnostics still worth reporting.</param>
/// <param name="SuppressedCount">How many were dropped, so the report can say so.</param>
public sealed record SuppressionResult(IReadOnlyList<Diagnostic> Kept, int SuppressedCount);
