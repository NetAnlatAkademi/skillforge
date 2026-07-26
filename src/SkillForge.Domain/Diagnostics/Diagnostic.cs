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
public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? FilePath = null,
    int? Line = null,
    string? Suggestion = null)
{
    /// <summary>Creates an <see cref="DiagnosticSeverity.Error"/> diagnostic.</summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Message for the user.</param>
    /// <param name="filePath">Optional file the finding refers to.</param>
    /// <param name="line">Optional one-based line number.</param>
    /// <param name="suggestion">Optional remediation hint.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Error(
        string code,
        string message,
        string? filePath = null,
        int? line = null,
        string? suggestion = null) =>
        new(code, DiagnosticSeverity.Error, message, filePath, line, suggestion);

    /// <summary>Creates a <see cref="DiagnosticSeverity.Warning"/> diagnostic.</summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Message for the user.</param>
    /// <param name="filePath">Optional file the finding refers to.</param>
    /// <param name="line">Optional one-based line number.</param>
    /// <param name="suggestion">Optional remediation hint.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Warning(
        string code,
        string message,
        string? filePath = null,
        int? line = null,
        string? suggestion = null) =>
        new(code, DiagnosticSeverity.Warning, message, filePath, line, suggestion);

    /// <summary>Creates an <see cref="DiagnosticSeverity.Info"/> diagnostic.</summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Message for the user.</param>
    /// <param name="filePath">Optional file the finding refers to.</param>
    /// <param name="line">Optional one-based line number.</param>
    /// <param name="suggestion">Optional remediation hint.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Info(
        string code,
        string message,
        string? filePath = null,
        int? line = null,
        string? suggestion = null) =>
        new(code, DiagnosticSeverity.Info, message, filePath, line, suggestion);
}
