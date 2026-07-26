using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Skills;

/// <summary>
/// Default <see cref="ISkillLoader"/>: locates <c>SKILL.md</c>, splits it, parses the frontmatter and
/// inventories the surrounding files.
/// </summary>
/// <remarks>
/// The loader answers one question — can this skill be modelled at all? It reports only the failures
/// that prevent that (<see cref="DiagnosticCodes.SkillFileNotFound"/>,
/// <see cref="DiagnosticCodes.FrontmatterNotFound"/>, <see cref="DiagnosticCodes.FrontmatterNotParsable"/>)
/// plus files it refused to read (<see cref="DiagnosticCodes.PathEscapesSkillDirectory"/>). Judgements
/// about quality — a missing name, a short description — belong to the validation rules, which run on
/// the model this loader produces.
/// </remarks>
public sealed class SkillLoader : ISkillLoader
{
    /// <summary>Directories that are never part of a skill.</summary>
    private static readonly string[] IgnoredDirectoryNames =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts"];

    private readonly IFileSystem _fileSystem;
    private readonly IFrontmatterParser _frontmatterParser;
    private readonly SkillPathGuard _pathGuard;

    /// <summary>Initialises the loader.</summary>
    /// <param name="fileSystem">File system used for all reads.</param>
    /// <param name="frontmatterParser">Parser used for the YAML block.</param>
    public SkillLoader(IFileSystem fileSystem, IFrontmatterParser frontmatterParser)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(frontmatterParser);

        _fileSystem = fileSystem;
        _frontmatterParser = frontmatterParser;
        _pathGuard = new SkillPathGuard(fileSystem);
    }

    /// <inheritdoc />
    public async Task<OperationResult<SkillDefinition>> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var location = ResolveLocation(path);
        if (location is null)
        {
            return OperationResult<SkillDefinition>.Failure(Diagnostic.Error(
                DiagnosticCodes.SkillFileNotFound,
                $"No {SkillDefinition.SkillFileName} was found at '{path}'.",
                suggestion: $"Point SkillForge at a directory containing {SkillDefinition.SkillFileName}, "
                    + $"or run 'skillforge init' to create one."));
        }

        var (directoryPath, skillFilePath) = location.Value;
        var content = await _fileSystem.ReadAllTextAsync(skillFilePath, cancellationToken)
            .ConfigureAwait(false);

        var split = FrontmatterSplitter.TrySplit(content);
        if (split is null)
        {
            return OperationResult<SkillDefinition>.Failure(Diagnostic.Error(
                DiagnosticCodes.FrontmatterNotFound,
                $"{SkillDefinition.SkillFileName} has no YAML frontmatter block.",
                SkillDefinition.SkillFileName,
                line: 1,
                suggestion: "Start the file with a '---' line, the skill's name and description, "
                    + "and a closing '---' line."));
        }

        var frontmatterResult = _frontmatterParser.Parse(
            split.Yaml,
            split.StartLine,
            SkillDefinition.SkillFileName);

        if (!frontmatterResult.IsSuccess || frontmatterResult.Value is null)
        {
            return OperationResult<SkillDefinition>.Failure(frontmatterResult.Diagnostics);
        }

        var diagnostics = new List<Diagnostic>(frontmatterResult.Diagnostics);
        var frontmatter = frontmatterResult.Value with
        {
            StartLine = split.StartLine,
            EndLine = split.EndLine,
        };

        var resources = CollectResources(directoryPath, diagnostics, cancellationToken);

        var definition = new SkillDefinition(
            Name: frontmatter.Name ?? string.Empty,
            Description: frontmatter.Description ?? string.Empty,
            DirectoryPath: directoryPath,
            SkillFilePath: skillFilePath,
            Frontmatter: frontmatter,
            Resources: resources,
            Body: split.Body,
            SkillFileLineCount: split.TotalLineCount);

        return OperationResult<SkillDefinition>.Success(definition, diagnostics);
    }

    /// <summary>
    /// Works out the skill directory and entry point from whatever the user pointed at.
    /// </summary>
    private (string DirectoryPath, string SkillFilePath)? ResolveLocation(string path)
    {
        var fullPath = _fileSystem.GetFullPath(path);

        if (_fileSystem.DirectoryExists(fullPath))
        {
            var candidate = _fileSystem.GetFullPath(Path.Combine(fullPath, SkillDefinition.SkillFileName));
            return _fileSystem.FileExists(candidate) ? (fullPath, candidate) : null;
        }

        if (!_fileSystem.FileExists(fullPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(directory) ? null : (_fileSystem.GetFullPath(directory), fullPath);
    }

    /// <summary>
    /// Inventories the files of the skill, skipping tooling directories and anything that would escape
    /// the skill root.
    /// </summary>
    private List<SkillResource> CollectResources(
        string directoryPath,
        List<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var resources = new List<SkillResource>();

        foreach (var absolutePath in _fileSystem.EnumerateFiles(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = _pathGuard.ToRelativePath(directoryPath, absolutePath);
            if (IsIgnored(relativePath))
            {
                continue;
            }

            if (!_pathGuard.IsInsideSkillDirectory(directoryPath, absolutePath))
            {
                diagnostics.Add(Diagnostic.Error(
                    DiagnosticCodes.PathEscapesSkillDirectory,
                    $"'{relativePath}' resolves to a location outside the skill directory and was not read.",
                    relativePath,
                    suggestion: "Keep every file a skill needs inside the skill directory. "
                        + "Links pointing outside it cannot be packaged or reviewed."));
                continue;
            }

            resources.Add(new SkillResource(
                RelativePath: relativePath,
                AbsolutePath: _fileSystem.GetFullPath(absolutePath),
                Kind: SkillResourceClassifier.Classify(relativePath),
                SizeInBytes: _fileSystem.GetFileSizeInBytes(absolutePath)));
        }

        // Ordering is part of the contract: reports and package contents must be reproducible.
        resources.Sort(static (left, right) =>
            string.CompareOrdinal(left.RelativePath, right.RelativePath));

        return resources;
    }

    private static bool IsIgnored(string relativePath)
    {
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return false; // Escaping paths are reported, not silently skipped.
        }

        var segments = relativePath.Split('/');
        return segments.Length > 1
            && segments[..^1].Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
