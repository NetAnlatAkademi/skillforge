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

    /// <inheritdoc />
    public void RenderRun(ValidationRun run, ReportRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Quiet)
        {
            _console.MarkupLine(Style(options.Title, "bold", options));
            _console.WriteLine();
            _console.MarkupLine($"Root:   {Escape(run.RootPath)}");
            _console.MarkupLine($"Skills: {run.SkillCount}");
        }

        foreach (var report in run.Skills)
        {
            // Each skill gets its own named block; in a batch, a finding without its skill's name beside it is
            // just noise.
            var name = report.SkillName.Length == 0 ? "(unnamed)" : report.SkillName;
            var location = SkillLocation(run.RootPath, report.SkillPath);

            // Usually the directory is named after the skill, and printing both reads as a stutter. Show the
            // location only when it tells the reader something the name does not.
            var heading = string.Equals(name, location, StringComparison.OrdinalIgnoreCase)
                ? Style(name, "bold", options)
                : Style(name, "bold", options) + $"  {Escape(location)}";

            _console.WriteLine();
            _console.MarkupLine(heading);

            var findings = options.Quiet
                ? report.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                : report.Diagnostics;

            foreach (var diagnostic in findings)
            {
                WriteDiagnostic(diagnostic, options);
            }

            if (report.Diagnostics.Count == 0)
            {
                _console.MarkupLine(Style("  ok", "green", options));
            }
        }

        _console.WriteLine();
        _console.MarkupLine(run.IsValid
            ? $"Result: {Style(run.Summary.Warnings > 0 ? "VALID WITH WARNINGS" : "VALID", run.Summary.Warnings > 0 ? "yellow" : "green", options)}"
            : $"Result: {Style("INVALID", "red", options)} — {run.InvalidSkillCount} of {run.SkillCount} skills have errors");

        _console.MarkupLine(
            $"Errors: {run.Summary.Errors}  "
            + $"Warnings: {run.Summary.Warnings}  "
            + $"Info: {run.Summary.Info}"
            + Suppressed(run.SuppressedCount));

        if (!options.Quiet)
        {
            // Across the whole batch, not per skill. A run over 229 skills that repeated the suppress hint 229 times
            // would be demonstrating the problem it is trying to solve.
            WriteNextSteps([.. run.Skills.SelectMany(report => report.Diagnostics)], options);
        }
    }

    /// <summary>
    /// Shows each skill relative to the root that was searched, since the absolute paths in a batch share a
    /// long prefix that tells the reader nothing.
    /// </summary>
    private static string SkillLocation(string rootPath, string skillPath)
    {
        var relative = Path.GetRelativePath(rootPath, skillPath).Replace('\\', '/');

        return relative is "." or "" ? skillPath : relative;
    }

    private void WriteHeader(ValidationReport report, ReportRenderOptions options)
    {
        var name = report.SkillName.Length == 0 ? "(unnamed)" : report.SkillName;

        _console.MarkupLine(Style(options.Title, "bold", options));
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

        // A fix is shown without asking for --verbose, and the suggestion still is not. The difference is what the
        // reader has to do with each: a fix is text to paste, and making somebody pass a flag to learn how to solve
        // a one-line problem is telling them what is wrong and leaving them to work out the schema. A suggestion is
        // prose about the reasoning, which is what --verbose is for.
        if (diagnostic.Fix is { Length: > 0 } fix)
        {
            WriteFix(fix, options);
        }

        if (options.Verbose && diagnostic.Suggestion is { Length: > 0 } suggestion)
        {
            _console.MarkupLine($"    -> {Escape(suggestion)}");
        }
    }

    /// <summary>
    /// Writes a fix under its finding, indented so a multi-line snippet keeps the shape it needs to be pasted in.
    /// </summary>
    private void WriteFix(string fix, ReportRenderOptions options)
    {
        var lines = fix.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var label = index == 0 ? Style("fix", "cyan", options) : "   ";
            _console.MarkupLine($"    {label}  {Escape(lines[index])}");
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
            + $"Info: {report.Summary.Info}"
            + Suppressed(report.SuppressedCount));

        if (!options.Quiet)
        {
            WriteNextSteps(report.Diagnostics, options);
        }
    }

    /// <summary>
    /// Closes the report by telling the reader what to do with it.
    /// </summary>
    /// <remarks>
    /// Two things only, both of which a reader would otherwise have to work out. How much of this list is trivially
    /// fixable, so a report of four warnings is not read as four problems. And the exact flag for the rules that
    /// fire on almost every skill: SF1009 and SF1010 are correct and were kept for that reason, but a reader meeting
    /// them for the first time deserves the escape hatch rather than a lecture about it in the documentation.
    ///
    /// Only codes actually present are named, because a suggestion to suppress something that did not fire is noise
    /// of exactly the kind this is meant to reduce.
    /// </remarks>
    private void WriteNextSteps(IReadOnlyList<Diagnostic> diagnostics, ReportRenderOptions options)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        var fixable = diagnostics.Count(diagnostic => diagnostic.Fix is { Length: > 0 });

        var noisy = AlwaysFiringCodes
            .Where(code => diagnostics.Any(d => string.Equals(d.Code, code, StringComparison.Ordinal)))
            .ToArray();

        if (fixable == 0 && noisy.Length == 0)
        {
            return;
        }

        _console.WriteLine();

        var first = true;
        void Line(string text)
        {
            _console.MarkupLine(first ? $"{Style("Next:", "bold", options)} {text}" : $"      {text}");
            first = false;
        }

        if (fixable > 0)
        {
            Line($"{fixable} of these {(fixable == 1 ? "has" : "have")} a fix printed above.");
        }

        if (noisy.Length > 0)
        {
            var one = noisy.Length == 1;
            Line($"{string.Join(" and ", noisy)} {(one ? "fires" : "fire")} on almost every skill. "
                + $"If {(one ? "it does" : "they do")} not");
            Line($"apply here, run with:  --suppress {string.Join(",", noisy)}");
        }
    }

    /// <summary>
    /// The rules measured to fire on approximately every real skill: SF1009 on 30 of 32 and SF1010 on 32 of 32 in
    /// the first sample, both on essentially all of a later 229. They were kept because they are right — see
    /// <c>docs/validation-rules.md</c> — which is exactly why the report should hand over the flag rather than
    /// pretend the reader will find it.
    /// </summary>
    private static readonly string[] AlwaysFiringCodes =
        [DiagnosticCodes.LicenseMissing, DiagnosticCodes.CompatibilityMissing];

    /// <summary>
    /// Suppression is never invisible. A report that quietly omitted findings would be lying about what was
    /// checked, and this number is what tells a reader to go and look at the configuration.
    /// </summary>
    private static string Suppressed(int count) => count == 0 ? string.Empty : $"  Suppressed: {count}";

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
