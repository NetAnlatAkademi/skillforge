using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Validation;
using Spectre.Console;

namespace SkillForge.Reporting;

/// <summary>
/// Renders a validation report as human-readable console output.
/// </summary>
/// <remarks>
/// Errors come first because they are what stops the user, and the summary comes last because that is
/// where the eye lands after a command finishes. Every diagnostic prints its code so a reader can look it
/// up or suppress it. Colour is a hint, never the only signal: each line carries a text marker too, so
/// the output still reads correctly in a log file or with <c>--no-color</c>.
/// </remarks>
public sealed class ConsoleReportRenderer : IValidationReportRenderer
{
    private readonly IAnsiConsole _console;

    /// <summary>Initialises the renderer against the shared console.</summary>
    public ConsoleReportRenderer()
        : this(AnsiConsole.Console)
    {
    }

    /// <summary>Initialises the renderer.</summary>
    /// <param name="console">Console to write to. Tests pass a recording console.</param>
    public ConsoleReportRenderer(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public void Render(ValidationReport report, ReportRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Quiet)
        {
            WriteHeader(report, options);
        }

        foreach (var diagnostic in report.Diagnostics)
        {
            if (options.Quiet && diagnostic.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            WriteDiagnostic(diagnostic, options);
        }

        WriteSummary(report, options);
    }

    private void WriteHeader(ValidationReport report, ReportRenderOptions options)
    {
        var name = report.SkillName.Length == 0 ? "(unnamed)" : report.SkillName;

        _console.MarkupLine(Style("SkillForge Validate", "bold", options));
        _console.WriteLine();
        _console.MarkupLine($"Skill: {Escape(name)}");
        _console.MarkupLine($"Path:  {Escape(report.SkillPath)}");
        _console.WriteLine();
    }

    private void WriteDiagnostic(Diagnostic diagnostic, ReportRenderOptions options)
    {
        var marker = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "x",
            DiagnosticSeverity.Warning => "!",
            _ => "i",
        };

        var colour = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "red",
            DiagnosticSeverity.Warning => "yellow",
            _ => "grey",
        };

        var location = FormatLocation(diagnostic);
        var suffix = location.Length == 0 ? string.Empty : $" ({Escape(location)})";

        _console.MarkupLine(
            $"{Style($"{marker} {diagnostic.Code}", colour, options)} {Escape(diagnostic.Message)}{suffix}");

        if (options.Verbose && diagnostic.Suggestion is { Length: > 0 } suggestion)
        {
            _console.MarkupLine($"    -> {Escape(suggestion)}");
        }
    }

    private void WriteSummary(ValidationReport report, ReportRenderOptions options)
    {
        var (verdict, colour) = report switch
        {
            { IsValid: false } => ("INVALID", "red"),
            { Summary.Warnings: > 0 } => ("VALID WITH WARNINGS", "yellow"),
            _ => ("VALID", "green"),
        };

        if (!options.Quiet)
        {
            _console.WriteLine();
        }

        _console.MarkupLine($"Result: {Style(verdict, colour, options)}");
        _console.MarkupLine(
            $"Errors: {report.Summary.Errors}  "
            + $"Warnings: {report.Summary.Warnings}  "
            + $"Info: {report.Summary.Info}");
    }

    private static string FormatLocation(Diagnostic diagnostic)
    {
        if (diagnostic.FilePath is not { Length: > 0 } path)
        {
            return string.Empty;
        }

        return diagnostic.Line is { } line ? $"{path}:{line}" : path;
    }

    /// <summary>
    /// Wraps text in Spectre markup, or leaves it plain when colour is suppressed. The text is escaped
    /// either way: a skill name or message may legitimately contain square brackets.
    /// </summary>
    private static string Style(string text, string style, ReportRenderOptions options) =>
        options.NoColor ? Escape(text) : $"[{style}]{Escape(text)}[/]";

    private static string Escape(string text) => Markup.Escape(text);
}
