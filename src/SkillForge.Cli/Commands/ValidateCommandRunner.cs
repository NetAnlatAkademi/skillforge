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
    private readonly IValidationReportRenderer _renderer;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads the skill.</param>
    /// <param name="validator">Runs the rules.</param>
    /// <param name="renderer">Presents the report.</param>
    /// <remarks>
    /// Public because the dependency injection container will only use a public constructor, even for an
    /// internal type.
    /// </remarks>
    public ValidateCommandRunner(
        ISkillLoader loader,
        ISkillValidator validator,
        IValidationReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(renderer);

        _loader = loader;
        _validator = validator;
        _renderer = renderer;
    }

    /// <summary>
    /// Loads a skill, validates it, presents the result and decides the exit code.
    /// </summary>
    /// <param name="path">Skill directory or <c>SKILL.md</c> path.</param>
    /// <param name="strict">When set, warnings fail as well as errors.</param>
    /// <param name="renderOptions">How to present the report.</param>
    /// <param name="cancellationToken">Token used to cancel the run.</param>
    /// <returns><see cref="ExitCodes.Success"/> or <see cref="ExitCodes.ValidationFailed"/>.</returns>
    internal async Task<int> RunAsync(
        string path,
        bool strict,
        ReportRenderOptions renderOptions,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);

        // A skill that cannot be loaded still gets a report: the user needs to see why, in the same shape
        // as any other failure, rather than a bare error line.
        if (!load.IsSuccess || load.Value is null)
        {
            var failure = ValidationReport.ForUnloadableSkill(path, load.Diagnostics);
            _renderer.Render(failure, renderOptions);
            return ExitCodes.ValidationFailed;
        }

        var report = await _validator.ValidateAsync(load.Value, cancellationToken).ConfigureAwait(false);

        // Diagnostics the loader produced belong in the report too — a duplicated frontmatter field is
        // not something the rules can see.
        if (load.Diagnostics.Count > 0)
        {
            report = report with
            {
                Diagnostics = DiagnosticOrdering.Sort([.. load.Diagnostics, .. report.Diagnostics]),
                Summary = ValidationSummary.FromDiagnostics([.. load.Diagnostics, .. report.Diagnostics]),
            };
        }

        _renderer.Render(report, renderOptions);

        return report.HasFailed(strict) ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }
}
