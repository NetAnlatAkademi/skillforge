using SkillForge.Application.Abstractions;
using SkillForge.Application.Packaging;
using SkillForge.Application.Validation;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge pack</c> does.
/// </summary>
/// <remarks>
/// Validation is a gate, not a suggestion: a skill with errors is not packaged unless the user explicitly
/// says otherwise with <c>--skip-validation</c>, and that choice is printed so it cannot be made silently in
/// a CI log.
/// </remarks>
internal sealed class PackCommandRunner
{
    private readonly ISkillLoader _loader;
    private readonly ISkillValidator _validator;
    private readonly ISkillPackager _packager;
    private readonly IValidationReportRenderer _renderer;

    /// <summary>Initialises the runner.</summary>
    /// <param name="loader">Loads the skill.</param>
    /// <param name="validator">Runs the validation gate.</param>
    /// <param name="packager">Builds the package.</param>
    /// <param name="renderer">Presents validation output.</param>
    public PackCommandRunner(
        ISkillLoader loader,
        ISkillValidator validator,
        ISkillPackager packager,
        IValidationReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(packager);
        ArgumentNullException.ThrowIfNull(renderer);

        _loader = loader;
        _validator = validator;
        _packager = packager;
        _renderer = renderer;
    }

    /// <summary>Packages a skill.</summary>
    /// <param name="request">What to package and where.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(PackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = await _loader.LoadAsync(request.Path, cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess || load.Value is null)
        {
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(request.Path, load.Diagnostics),
                request.RenderOptions);

            return ExitCodes.ValidationFailed;
        }

        var report = await _validator.ValidateAsync(load.Value, cancellationToken).ConfigureAwait(false);

        if (!report.IsValid)
        {
            _renderer.Render(report, request.RenderOptions);

            if (!request.SkipValidation)
            {
                await Console.Error.WriteLineAsync(
                    "Not packaging a skill with errors. Fix them, or pass --skip-validation deliberately.")
                    .ConfigureAwait(false);

                return ExitCodes.ValidationFailed;
            }

            await Console.Out.WriteLineAsync("Packaging anyway because --skip-validation was given.")
                .ConfigureAwait(false);
        }

        var packed = await _packager
            .PackAsync(load.Value, request.OutputDirectory, request.VersionOverride, cancellationToken)
            .ConfigureAwait(false);

        if (!packed.IsSuccess || packed.Value is null)
        {
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(request.Path, packed.Diagnostics),
                request.RenderOptions);

            return ExitCodes.ValidationFailed;
        }

        if (!request.RenderOptions.Quiet)
        {
            var package = packed.Value;
            await Console.Out.WriteLineAsync($"Packaged {package.SkillName} {package.Version}")
                .ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"  {package.ArchivePath}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"  {package.HashPath}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"  {package.ManifestPath}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"sha256: {package.ArchiveSha256}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"files:  {package.Files.Count}").ConfigureAwait(false);
        }

        return ExitCodes.Success;
    }
}
