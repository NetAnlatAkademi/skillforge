using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// Something worth pointing out when it appears in a skill, with the reason a reader should care.
/// </summary>
/// <remarks>
/// Shared by the shell-privilege and activation-risk rules. Named generically because a second consumer arrived:
/// both are "recognise a construct, explain why it matters, never conclude the skill is unsafe" (ADR-006).
/// </remarks>
/// <param name="Name">Short name used in the diagnostic message.</param>
/// <param name="Pattern">Expression that recognises it.</param>
/// <param name="Why">What a reader should think about when they see it.</param>
public sealed record RiskPattern(string Name, Regex Pattern, string Why);
