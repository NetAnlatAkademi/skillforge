using SkillForge.Application.Abstractions;
using SkillForge.Application.Validation;
using SkillForge.Domain.Skills;
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
    private readonly ISkillDiscovery _discovery;
    private readonly IFileSystem _fileSystem;
    private readonly ReportOutput _output;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads a skill.</param>
    /// <param name="validator">Runs the rules.</param>
    /// <param name="discovery">Finds the skills when the path holds several of them.</param>
    /// <param name="fileSystem">Used to tell one skill from a directory of skills.</param>
    /// <param name="output">Writes the report where the user asked for it.</param>
    /// <remarks>
    /// Public because the dependency injection container will only use a public constructor, even for an
    /// internal type.
    /// </remarks>
    public ValidateCommandRunner(
        ISkillLoader loader,
        ISkillValidator validator,
        ISkillDiscovery discovery,
        IFileSystem fileSystem,
        ReportOutput output)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(output);

        _loader = loader;
        _validator = validator;
        _discovery = discovery;
        _fileSystem = fileSystem;
        _output = output;
    }

    /// <summary>
    /// Loads a skill, validates it, presents the result and decides the exit code.
    /// </summary>
    /// <param name="request">What to validate and how to report it.</param>
    /// <param name="cancellationToken">Token used to cancel the run.</param>
    /// <returns><see cref="ExitCodes.Success"/> or <see cref="ExitCodes.ValidationFailed"/>.</returns>
    internal async Task<int> RunAsync(ValidateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A directory holding several skills is validated as a batch. Nobody should have to write a shell loop
        // for the ordinary case of "check the skills in this repository", and one SARIF file covering every
        // skill is what a code-scanning upload actually wants.
        var skillDirectories = IsSingleSkill(request.Path)
            ? []
            : _discovery.FindSkillDirectories(request.Path);

        return skillDirectories.Count > 0
            ? await RunBatchAsync(request, skillDirectories, cancellationToken).ConfigureAwait(false)
            : await RunSingleAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decides whether the path names one skill. A path that is a file, or a directory with its own
    /// <c>SKILL.md</c>, is one skill even when it happens to contain others.
    /// </summary>
    private bool IsSingleSkill(string path)
    {
        var fullPath = _fileSystem.GetFullPath(path);

        return _fileSystem.FileExists(fullPath)
            || _fileSystem.FileExists(Path.Combine(fullPath, SkillDefinition.SkillFileName));
    }

    private async Task<int> RunSingleAsync(ValidateRequest request, CancellationToken cancellationToken)
    {
        var report = await BuildReportAsync(request.Path, cancellationToken).ConfigureAwait(false);

        await _output.WriteAsync(
            report,
            request.Format,
            request.OutputPath,
            request.RenderOptions,
            cancellationToken).ConfigureAwait(false);

        return report.HasFailed(request.Strict) ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    private async Task<int> RunBatchAsync(
        ValidateRequest request,
        IReadOnlyList<string> skillDirectories,
        CancellationToken cancellationToken)
    {
        var reports = new List<ValidationReport>(skillDirectories.Count);

        // Sequential on purpose: the output has to come out in discovery order for a run to be reproducible,
        // and validation is light enough that parallelism would buy noise rather than speed.
        foreach (var directory in skillDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            reports.Add(await BuildReportAsync(directory, cancellationToken).ConfigureAwait(false));
        }

        var run = ValidationRun.From(_fileSystem.GetFullPath(request.Path), reports);

        await _output.WriteRunAsync(
            run,
            request.Format,
            request.OutputPath,
            request.RenderOptions,
            cancellationToken).ConfigureAwait(false);

        return run.HasFailed(request.Strict) ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    private async Task<ValidationReport> BuildReportAsync(
        string skillPath,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(skillPath, cancellationToken).ConfigureAwait(false);

        // A skill that cannot be loaded still gets a report: the user needs to see why, in the same shape
        // as any other failure, rather than a bare error line.
        if (!load.IsSuccess || load.Value is null)
        {
            return ValidationReport.ForUnloadableSkill(skillPath, load.Diagnostics);
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
