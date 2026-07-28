using SkillForge.Application.Abstractions;
using SkillForge.Application.Providers;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
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
    private readonly IProviderCompatibilityChecker _providerChecker;
    private readonly ReportOutput _output;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads a skill.</param>
    /// <param name="validator">Runs the rules.</param>
    /// <param name="discovery">Finds the skills when the path holds several of them.</param>
    /// <param name="fileSystem">Used to tell one skill from a directory of skills.</param>
    /// <param name="providerChecker">
    /// Checks the skill against the providers it declares, plus any given with <c>--provider</c>. Separate from
    /// the rule set because it needs the run's options, not just the skill.
    /// </param>
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
        IProviderCompatibilityChecker providerChecker,
        ReportOutput output)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(providerChecker);
        ArgumentNullException.ThrowIfNull(output);

        _loader = loader;
        _validator = validator;
        _discovery = discovery;
        _fileSystem = fileSystem;
        _providerChecker = providerChecker;
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
        var result = await BuildReportAsync(request.Path, request, cancellationToken).ConfigureAwait(false);

        await _output.WriteAsync(
            result.Report,
            request.Format,
            request.OutputPath,
            request.RenderOptions,
            cancellationToken).ConfigureAwait(false);

        return result.HasFailed ? ExitCodes.ValidationFailed : ExitCodes.Success;
    }

    private async Task<int> RunBatchAsync(
        ValidateRequest request,
        IReadOnlyList<string> skillDirectories,
        CancellationToken cancellationToken)
    {
        var results = new List<SkillResult>(skillDirectories.Count);

        // Sequential on purpose: the output has to come out in discovery order for a run to be reproducible,
        // and validation is light enough that parallelism would buy noise rather than speed.
        foreach (var directory in skillDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(await BuildReportAsync(directory, request, cancellationToken).ConfigureAwait(false));
        }

        var run = ValidationRun.From(
            _fileSystem.GetFullPath(request.Path),
            [.. results.Select(result => result.Report)]);

        await _output.WriteRunAsync(
            run,
            request.Format,
            request.OutputPath,
            request.RenderOptions,
            cancellationToken).ConfigureAwait(false);

        // Each skill is judged against its own effective strictness, because strict can come from that skill's
        // own skillforge.yaml. One failing skill fails the run.
        return results.Exists(result => result.HasFailed)
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    /// <summary>
    /// A skill's report together with the strictness it was judged against — which may be its own, from
    /// <c>skillforge.yaml</c>, rather than the run's.
    /// </summary>
    private sealed record SkillResult(ValidationReport Report, bool Strict)
    {
        internal bool HasFailed => Report.HasFailed(Strict);
    }

    private async Task<SkillResult> BuildReportAsync(
        string skillPath,
        ValidateRequest request,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(skillPath, cancellationToken).ConfigureAwait(false);

        // The loader reads the skill's skillforge.yaml, so a load failure means there is no per-skill
        // configuration to honour. --suppress still applies, which is the only way to silence a load failure.
        var configuration = load.Value?.Configuration ?? SkillConfiguration.Default;

        // The flag and the file add up: a repository-wide decision and a per-skill one are different decisions,
        // so neither silently cancels the other. --strict forces strictness on; a skill can also ask for it.
        var suppressed = request.SuppressedCodes.Concat(configuration.SuppressedCodes).ToArray();
        var strict = request.Strict || configuration.Strict;

        // A skill that cannot be loaded still gets a report: the user needs to see why, in the same shape
        // as any other failure, rather than a bare error line.
        if (!load.IsSuccess || load.Value is null)
        {
            return new SkillResult(
                Finish(
                    ValidationReport.ForUnloadableSkill(skillPath, load.Diagnostics),
                    load.Diagnostics,
                    suppressed),
                strict);
        }

        var report = await _validator.ValidateAsync(load.Value, cancellationToken).ConfigureAwait(false);

        // The provider checks are not rules: they depend on --provider as well as on the skill. They are merged
        // here for the same reason the loader's diagnostics are — the report is the one place a user looks.
        var providerFindings = _providerChecker.Check(load.Value, request.Providers);

        // Loader diagnostics belong in the report too — a duplicated frontmatter field, or a skillforge.yaml that
        // had to be ignored, is not something the rules can see.
        return new SkillResult(
            Finish(report, [.. load.Diagnostics, .. report.Diagnostics, .. providerFindings], suppressed),
            strict);
    }

    /// <summary>
    /// Applies suppression, re-orders and re-counts, so the summary reflects exactly what is reported.
    /// </summary>
    private static ValidationReport Finish(
        ValidationReport report,
        IReadOnlyList<Diagnostic> allDiagnostics,
        IReadOnlyCollection<string> suppressedCodes)
    {
        var suppression = DiagnosticSuppression.Apply(allDiagnostics, suppressedCodes);
        var ordered = DiagnosticOrdering.Sort(suppression.Kept);

        return report with
        {
            Diagnostics = ordered,
            Summary = ValidationSummary.FromDiagnostics(ordered),
            SuppressedCount = suppression.SuppressedCount,
        };
    }

}
