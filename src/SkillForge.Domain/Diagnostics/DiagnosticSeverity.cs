namespace SkillForge.Domain.Diagnostics;

/// <summary>
/// How seriously a <see cref="Diagnostic"/> should be treated.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>A neutral observation about the skill. Never fails a build.</summary>
    Info = 0,

    /// <summary>The skill works, but quality or risk deserves attention. Fails only in strict mode.</summary>
    Warning = 1,

    /// <summary>The skill is not usable as written. Always fails validation.</summary>
    Error = 2,
}
