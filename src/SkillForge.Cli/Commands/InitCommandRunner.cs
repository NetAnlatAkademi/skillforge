using System.IO;
using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Cli.Commands;

/// <summary>
/// What <c>skillforge init</c> actually does.
/// </summary>
/// <remarks>
/// Refusing to overwrite is policy, so it lives here rather than in the initializer: the check happens
/// before anything is written, and an existing directory without <c>--force</c> is a usage error rather
/// than a finding about a skill.
/// </remarks>
internal sealed class InitCommandRunner
{
    private readonly IFileSystem _fileSystem;
    private readonly ISkillInitializer _initializer;
    private readonly ValidateCommandRunner _validate;
    private readonly IValidationReportRenderer _renderer;

    /// <summary>Initialises the runner.</summary>
    /// <param name="fileSystem">Used to check whether the target already exists.</param>
    /// <param name="initializer">Creates the skill.</param>
    /// <param name="validate">Validates the generated skill afterwards.</param>
    /// <param name="renderer">Used to report a refusal.</param>
    public InitCommandRunner(
        IFileSystem fileSystem,
        ISkillInitializer initializer,
        ValidateCommandRunner validate,
        IValidationReportRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(renderer);

        _fileSystem = fileSystem;
        _initializer = initializer;
        _validate = validate;
        _renderer = renderer;
    }

    /// <summary>
    /// Creates a skill and validates what it created.
    /// </summary>
    /// <param name="request">What to create and where.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> RunAsync(InitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = request.Directory ?? request.Options.Name;
        var fullPath = _fileSystem.GetFullPath(target);
        var skillFile = Path.Combine(fullPath, SkillDefinition.SkillFileName);

        if (!request.Options.Force && _fileSystem.FileExists(skillFile))
        {
            await Console.Error.WriteLineAsync(
                $"'{fullPath}' already contains a {SkillDefinition.SkillFileName}. "
                + "Pass --force to overwrite it.").ConfigureAwait(false);

            return ExitCodes.InvalidUsage;
        }

        var result = await _initializer
            .InitializeAsync(fullPath, request.Options, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess || result.Value is null)
        {
            // An unusable name is the user's mistake, so it reads as a usage error even though it arrives
            // as a diagnostic.
            _renderer.Render(
                ValidationReport.ForUnloadableSkill(fullPath, result.Diagnostics),
                request.RenderOptions);

            return ExitCodes.InvalidUsage;
        }

        if (!request.RenderOptions.Quiet)
        {
            await Console.Out.WriteLineAsync(
                $"Created skill '{request.Options.Name}' in {result.Value.DirectoryPath}").ConfigureAwait(false);

            foreach (var file in result.Value.CreatedFiles)
            {
                await Console.Out.WriteLineAsync(
                    $"  {Path.GetRelativePath(fullPath, file).Replace('\\', '/')}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);
        }

        // A generated skill that does not pass validation would be a bug in the template, so the check runs
        // every time rather than being left to the user.
        return await _validate.RunAsync(
            new ValidateRequest(
                fullPath,
                Strict: false,
                OutputFormat.Console,
                OutputPath: null,
                request.RenderOptions,
                SuppressedCodes: []),
            cancellationToken).ConfigureAwait(false);
    }
}
