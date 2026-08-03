using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Application.Policy;
using SkillForge.Application.Provenance;
using SkillForge.Application.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Policy;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge policy check</c> does.
/// </summary>
/// <remarks>
/// The only command that judges rather than describes, and it judges only what an organisation wrote down. A
/// policy that decides nothing produces nothing, so adopting the command cannot start failing builds over a
/// decision nobody made.
///
/// A policy that could not be read fails the run with <c>SF9001</c> and checks nothing. The alternative —
/// carrying on with no policy — is a green build that means the opposite of what a reader would take it to mean.
///
/// Findings are reported as a validation run, so console, JSON and SARIF, suppression counts and exit codes all
/// behave exactly as they do for <c>validate</c>. A policy violation is a finding; there is no reason to invent a
/// second shape for it.
/// </remarks>
internal sealed class PolicyCheckCommandRunner
{
    private readonly IPolicyReader _policyReader;
    private readonly ISkillLoader _loader;
    private readonly ISkillInspector _inspector;
    private readonly ISkillDiscovery _discovery;
    private readonly IProvenanceReader _provenanceReader;
    private readonly IFileSystem _fileSystem;
    private readonly IValidationReportRenderer _renderer;
    private readonly ReportOutput _output;

    /// <summary>Initialises the runner.</summary>
    /// <param name="policyReader">Reads the organisation's policy.</param>
    /// <param name="loader">Loads each skill.</param>
    /// <param name="inspector">Computes what each skill's contents imply it can do.</param>
    /// <param name="discovery">Finds the skills when the path holds several of them.</param>
    /// <param name="provenanceReader">Records where each skill came from.</param>
    /// <param name="fileSystem">Used to tell one skill from a directory of skills.</param>
    /// <param name="renderer">Reports a policy that could not be read.</param>
    /// <param name="output">Writes the report where the user asked for it.</param>
    /// <remarks>
    /// Public because the dependency injection container will only use a public constructor, even for an
    /// internal type.
    /// </remarks>
    public PolicyCheckCommandRunner(
        IPolicyReader policyReader,
        ISkillLoader loader,
        ISkillInspector inspector,
        ISkillDiscovery discovery,
        IProvenanceReader provenanceReader,
        IFileSystem fileSystem,
        IValidationReportRenderer renderer,
        ReportOutput output)
    {
        ArgumentNullException.ThrowIfNull(policyReader);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(provenanceReader);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(output);

        _policyReader = policyReader;
        _loader = loader;
        _inspector = inspector;
        _discovery = discovery;
        _provenanceReader = provenanceReader;
        _fileSystem = fileSystem;
        _renderer = renderer;
        _output = output;
    }

    /// <summary>Judges one skill, or every skill under a path, against a policy.</summary>
    /// <param name="request">What to check and how to report it.</param>
    /// <param name="cancellationToken">Token used to cancel the run.</param>
    /// <returns><see cref="ExitCodes.Success"/> or <see cref="ExitCodes.ValidationFailed"/>.</returns>
    internal async Task<int> RunAsync(PolicyCheckRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The report shape is validate's; the heading must not be, or the reader is told the wrong thing about
        // what was checked.
        request = request with { RenderOptions = request.RenderOptions with { Title = "SkillForge Policy" } };

        var policyResult = await _policyReader
            .ReadAsync(request.PolicyPath, cancellationToken)
            .ConfigureAwait(false);

        if (!policyResult.IsSuccess || policyResult.Value is null)
        {
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(request.PolicyPath, policyResult.Diagnostics),
                request.RenderOptions);

            return ExitCodes.ValidationFailed;
        }

        var policy = policyResult.Value;

        var reports = new List<ValidationReport>();
        foreach (var skillPath in SkillPaths(request.Path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            reports.Add(await CheckAsync(skillPath, policy, cancellationToken).ConfigureAwait(false));
        }

        // The policy's own findings — a suppression with no reason, a rule this command cannot check — belong to
        // the policy file, not to any skill, so they are reported against the policy itself.
        var policyFindings = DiagnosticOrdering.Sort(
            [.. policyResult.Diagnostics, .. PolicyEvaluator.DescribeUnevaluatedRules(policy)]);

        if (policyFindings.Count > 0)
        {
            reports.Insert(0, new ValidationReport(
                string.Empty,
                request.PolicyPath,
                policyFindings,
                ValidationSummary.FromDiagnostics(policyFindings)));
        }

        var run = ValidationRun.From(_fileSystem.GetFullPath(request.Path), reports);

        await _output.WriteRunAsync(
            run,
            request.Format,
            request.OutputPath,
            request.RenderOptions,
            cancellationToken).ConfigureAwait(false);

        return reports.Exists(report => report.HasFailed(strict: false))
            ? ExitCodes.ValidationFailed
            : ExitCodes.Success;
    }

    /// <summary>
    /// Resolves the path to the skills it names. A path that is a file, or a directory with its own
    /// <c>SKILL.md</c>, is one skill even when it happens to contain others — the same rule <c>validate</c> uses.
    /// </summary>
    private IReadOnlyList<string> SkillPaths(string path)
    {
        var fullPath = _fileSystem.GetFullPath(path);

        if (_fileSystem.FileExists(fullPath)
            || _fileSystem.FileExists(Path.Combine(fullPath, SkillDefinition.SkillFileName)))
        {
            return [path];
        }

        var discovered = _discovery.FindSkillDirectories(path);

        return discovered.Count > 0 ? discovered : [path];
    }

    private async Task<ValidationReport> CheckAsync(
        string skillPath,
        PolicyDocument policy,
        CancellationToken cancellationToken)
    {
        var load = await _loader.LoadAsync(skillPath, cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess || load.Value is null)
        {
            // A skill that will not load cannot be judged, and saying nothing about it would let it through a
            // policy gate. It is reported exactly as any other command reports it.
            return ValidationReport.ForUnloadableSkill(skillPath, load.Diagnostics);
        }

        var skill = load.Value;

        var inspection = await _inspector.InspectAsync(skill, cancellationToken).ConfigureAwait(false);
        var provenance = await _provenanceReader
            .ReadAsync(skill.DirectoryPath, cancellationToken)
            .ConfigureAwait(false);

        var findings = PolicyEvaluator.Evaluate(
            policy,
            new PolicySubject(skill, inspection, skill.Configuration, provenance));

        return ValidationReport.For(skill, findings);
    }
}
