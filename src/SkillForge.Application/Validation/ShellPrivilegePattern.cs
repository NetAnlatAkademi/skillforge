using System.Text.RegularExpressions;

namespace SkillForge.Application.Validation;

/// <summary>
/// A shell construct worth pointing out when it appears in a skill's script.
/// </summary>
/// <param name="Name">Short name used in the diagnostic message.</param>
/// <param name="Pattern">Expression that recognises it.</param>
/// <param name="Why">What a reader should think about when they see it.</param>
public sealed record ShellPrivilegePattern(string Name, Regex Pattern, string Why);
