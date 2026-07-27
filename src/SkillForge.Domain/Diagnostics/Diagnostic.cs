namespace SkillForge.Domain.Diagnostics;

/// <summary>
/// A single finding about a skill: what is wrong, how serious it is, and where it was found.
/// </summary>
/// <param name="Code">Stable diagnostic code, for example <c>SF0003</c>. Codes are never reused.</param>
/// <param name="Severity">How seriously the finding should be treated.</param>
/// <param name="Message">
/// One sentence written for the person running the CLI, describing what was found.
/// </param>
/// <param name="FilePath">
/// Path of the file the finding refers to, relative to the skill directory, or <see langword="null"/>
/// when the finding is about the skill as a whole.
/// </param>
/// <param name="Line">One-based line number within <paramref name="FilePath"/>, when known.</param>
/// <param name="Suggestion">Concrete action the user can take to resolve the finding.</param>
/// <param name="Fix">
/// The literal text that resolves the finding, ready to be copied — YAML to add to the frontmatter, a file to
/// create. <see langword="null"/> when no single edit resolves it, which is most findings: SF1007 points at a
/// script that reaches further than usual and only a human can decide what should replace it.
///
/// Separate from <paramref name="Suggestion"/> on purpose. A suggestion is prose about what to do; a fix is
/// something a reader can paste. Rules that can compute one supply it per finding rather than per code, so
/// SF1006 can name the interpreters the skill's own scripts actually need.
/// </param>
public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? FilePath = null,
    int? Line = null,
    string? Suggestion = null,
    string? Fix = null)
{
    /// <summary>Creates an <see cref="DiagnosticSeverity.Error"/> diagnostic.</summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Message for the user.</param>
    /// <param name="filePath">Optional file the finding refers to.</param>
    /// <param name="line">Optional one-based line number.</param>
    /// <param name="suggestion">Optional remediation hint.</param>
    /// <param name="fix">Optional literal text that resolves the finding.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Error(
        string code,
        string message,
        string? filePath = null,
        int? line = null,
        string? suggestion = null,
        string? fix = null) =>
        new(code, DiagnosticSeverity.Error, message, filePath, line, suggestion, fix);

    /// <summary>Creates a <see cref="DiagnosticSeverity.Warning"/> diagnostic.</summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Message for the user.</param>
    /// <param name="filePath">Optional file the finding refers to.</param>
    /// <param name="line">Optional one-based line number.</param>
    /// <param name="suggestion">Optional remediation hint.</param>
    /// <param name="fix">Optional literal text that resolves the finding.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Warning(
        string code,
        string message,
        string? filePath = null,
        int? line = null,
        string? suggestion = null,
        string? fix = null) =>
        new(code, DiagnosticSeverity.Warning, message, filePath, line, suggestion, fix);

    /// <summary>Creates an <see cref="DiagnosticSeverity.Info"/> diagnostic.</summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Message for the user.</param>
    /// <param name="filePath">Optional file the finding refers to.</param>
    /// <param name="line">Optional one-based line number.</param>
    /// <param name="suggestion">Optional remediation hint.</param>
    /// <param name="fix">Optional literal text that resolves the finding.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Info(
        string code,
        string message,
        string? filePath = null,
        int? line = null,
        string? suggestion = null,
        string? fix = null) =>
        new(code, DiagnosticSeverity.Info, message, filePath, line, suggestion, fix);
}
