using SkillForge.Application.Abstractions;
using SkillForge.Application.Validation;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge validate</c> actually does, with no command line plumbing attached.
/// </summary>
/// <remarks>
/// Kept separate from the command definition so the behaviour that matters — which exit code comes out of
/// which situation — is testable without parsing arguments or starting a process.
/// </remarks>
internal sealed class ValidateCommandRunner
{
    private readonly ISkillLoader _loader;
    private readonly ISkillValidator _validator;
    private readonly ReportOutput _output;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads the skill.</param>
    /// <param name="validator">Runs the rules.</param>
    /// <param name="output">Writes the report where the user asked for it.</param>
    /// <remarks>
    /// Public because the dependency injection container will only use a public constructor, even for an
    /// internal type.
    /// </remarks>
    public ValidateCommandRunner(ISkillLoader loader, ISkillValidator validator, ReportOutput output)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(output);

        _loader = loader;
        _validator = validator;
        _output = output;
    }

    /// <summary>
    /// Loads a skill, validates it, presents the result and decides the exit code.
    /// </summary>
    /// <param name="request">What to validate and how to report it.</param>
    /// <param name="cancellationToken">Token used to cancel the run.</param>
    /// <returns><see cref="ExitCodes.Success"/> or <see cref="ExitCodes.ValidationFailed"/>.</returns>
    internal async Task<int> RunAsync(ValidateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await BuildReportAsync(request, cancellationToken).ConfigureAwait(false);

        await _output.WriteAsync(
            report,
            request.Format,
            request.OutputPath,
            request.RenderOptions,
            cancellationToken).ConfigureAwait(false);

        return report.HasFailed(request.Strict) ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    private async Task<ValidationReport> BuildReportAsync(
        ValidateRequest request,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(request.Path, cancellationToken).ConfigureAwait(false);

        // A skill that cannot be loaded still gets a report: the user needs to see why, in the same shape
        // as any other failure, rather than a bare error line.
        if (!load.IsSuccess || load.Value is null)
        {
            return ValidationReport.ForUnloadableSkill(request.Path, load.Diagnostics);
        }

        var report = await _validator.ValidateAsync(load.Value, cancellationToken).ConfigureAwait(false);

        if (load.Diagnostics.Count == 0)
        {
            return report;
        }

        // Diagnostics the loader produced belong in the report too — a duplicated frontmatter field is
        // not something the rules can see.
        var combined = DiagnosticOrdering.Sort([.. load.Diagnostics, .. report.Diagnostics]);

        return report with
        {
            Diagnostics = combined,
            Summary = ValidationSummary.FromDiagnostics(combined),
        };
    }
}

/// <summary>
/// Everything <c>validate</c> was asked to do.
/// </summary>
/// <param name="Path">Skill directory or <c>SKILL.md</c> path.</param>
/// <param name="Strict">When set, warnings fail as well as errors.</param>
/// <param name="Format">One of <see cref="OutputFormat"/>.</param>
/// <param name="OutputPath">File to write machine-readable output to, or <see langword="null"/> for stdout.</param>
/// <param name="RenderOptions">How to present console output.</param>
internal sealed record ValidateRequest(
    string Path,
    bool Strict,
    string Format,
    string? OutputPath,
    ReportRenderOptions RenderOptions);
