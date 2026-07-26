using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Skills;

/// <summary>
/// Creates a new skill directory on disk.
/// </summary>
/// <remarks>
/// Whether it is acceptable to write into an existing directory is a policy question the caller answers
/// with <see cref="SkillInitializationOptions.Force"/>. This class only refuses what it cannot do
/// correctly: an unusable name.
/// </remarks>
public sealed class SkillInitializer : ISkillInitializer
{
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the initializer.</summary>
    /// <param name="fileSystem">File system used for all writes.</param>
    public SkillInitializer(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<OperationResult<SkillInitializationResult>> InitializeAsync(
        string targetDirectory,
        SkillInitializationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentNullException.ThrowIfNull(options);

        if (SkillName.DescribeProblem(options.Name) is { } problem)
        {
            return OperationResult<SkillInitializationResult>.Failure(Diagnostic.Error(
                DiagnosticCodes.NameInvalid,
                $"'{options.Name}' cannot be used as a skill name: {problem}",
                suggestion: "Use lowercase letters, digits and single hyphens, starting with a letter — "
                    + "for example 'dotnet-api-review'."));
        }

        var root = _fileSystem.GetFullPath(targetDirectory);
        var template = new SkillTemplateOptions(
            options.Name,
            options.Description,
            options.Author,
            options.License,
            options.Version);

        _fileSystem.CreateDirectory(root);
        var created = new List<string>();

        foreach (var directory in SkillTemplate.Directories)
        {
            _fileSystem.CreateDirectory(Path.Combine(root, directory));

            // Git does not track empty directories, so each one gets a file explaining what belongs in it.
            var readme = Path.Combine(root, directory, "README.md");
            await WriteAsync(readme, DescribeDirectory(directory), created, cancellationToken)
                .ConfigureAwait(false);
        }

        await WriteAsync(
            Path.Combine(root, SkillDefinition.SkillFileName),
            SkillTemplate.CreateSkillFile(template),
            created,
            cancellationToken).ConfigureAwait(false);

        await WriteAsync(
            Path.Combine(root, SkillDefinition.ConfigurationFileName),
            SkillTemplate.CreateConfigurationFile(template),
            created,
            cancellationToken).ConfigureAwait(false);

        created.Sort(StringComparer.Ordinal);

        return OperationResult<SkillInitializationResult>.Success(
            new SkillInitializationResult(root, created));
    }

    private async Task WriteAsync(
        string path,
        string content,
        List<string> created,
        CancellationToken cancellationToken)
    {
        await _fileSystem.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        created.Add(_fileSystem.GetFullPath(path));
    }

    private static string DescribeDirectory(string directory) => directory switch
    {
        "references" => "# References\n\nMarkdown the agent reads only when the skill needs it.\n",
        "scripts" => "# Scripts\n\nExecutable helpers. Declare what they need in `skillforge.yaml`.\n",
        "assets" => "# Assets\n\nImages and other binary files the skill refers to.\n",
        _ => "# Evals\n\nCases that check the skill activates when it should, and not when it should not.\n",
    };
}
