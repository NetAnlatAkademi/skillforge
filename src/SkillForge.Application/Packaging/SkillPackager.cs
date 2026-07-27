using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Packaging;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Packaging;

/// <summary>
/// Turns a skill directory into a distributable package.
/// </summary>
/// <remarks>
/// Determinism is the whole point: the same skill contents must produce the same archive bytes and therefore
/// the same hash, on any machine. Files are sorted, the archive writer pins timestamps, and the only thing
/// that varies between builds — the creation time — lives in the manifest rather than in the archive.
/// </remarks>
public sealed class SkillPackager : ISkillPackager
{
    private static readonly JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };

    /// <summary>Directories never included in a package.</summary>
    private static readonly string[] ExcludedDirectories =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts", "dist"];

    /// <summary>Files never included in a package.</summary>
    private static readonly string[] ExcludedFiles = [".DS_Store", "Thumbs.db"];

    /// <summary>Version used when a skill declares none and no override is supplied.</summary>
    private const string DefaultVersion = "0.1.0";

    /// <summary>File extension for the packaged archive.</summary>
    private const string ArchiveSuffix = ".skill.zip";

    /// <summary>File extension for the archive's checksum file.</summary>
    private const string HashSuffix = ".sha256";

    /// <summary>File extension for the package manifest.</summary>
    private const string ManifestSuffix = ".manifest.json";

    private readonly IFileSystem _fileSystem;
    private readonly IArchiveWriter _archiveWriter;
    private readonly IHashCalculator _hashCalculator;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initialises the packager.</summary>
    /// <param name="fileSystem">Reads the skill and writes the artefacts.</param>
    /// <param name="archiveWriter">Builds the archive.</param>
    /// <param name="hashCalculator">Hashes contents.</param>
    /// <param name="timeProvider">Supplies the manifest timestamp. Injected so tests can pin it.</param>
    public SkillPackager(
        IFileSystem fileSystem,
        IArchiveWriter archiveWriter,
        IHashCalculator hashCalculator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(archiveWriter);
        ArgumentNullException.ThrowIfNull(hashCalculator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _fileSystem = fileSystem;
        _archiveWriter = archiveWriter;
        _hashCalculator = hashCalculator;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<OperationResult<SkillPackage>> PackAsync(
        SkillDefinition skill,
        string outputDirectory,
        string? versionOverride,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var version = versionOverride ?? skill.Frontmatter.Version ?? DefaultVersion;
        if (!IsUsableVersion(version))
        {
            return OperationResult<SkillPackage>.Failure(Diagnostic.Error(
                DiagnosticCodes.PackageVersionInvalid,
                $"'{version}' cannot be used as a package version.",
                SkillDefinition.SkillFileName,
                suggestion: "Set 'metadata.version' to a semantic version such as 1.0.0, "
                    + "or pass --version-override."));
        }

        var included = skill.Resources
            .Where(resource => !IsExcluded(resource.RelativePath))
            .OrderBy(resource => resource.RelativePath, StringComparer.Ordinal)
            .ToArray();

        if (included.Length == 0)
        {
            return OperationResult<SkillPackage>.Failure(Diagnostic.Error(
                DiagnosticCodes.SkillFileNotFound,
                "There is nothing to package: no files were found in the skill directory.",
                suggestion: $"A package needs at least a {SkillDefinition.SkillFileName}."));
        }

        var entries = new List<ArchiveEntry>(included.Length);
        var packagedFiles = new List<PackagedFile>(included.Length);

        foreach (var resource in included)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await _fileSystem
                .ReadAllBytesAsync(resource.AbsolutePath, cancellationToken)
                .ConfigureAwait(false);

            entries.Add(new ArchiveEntry(resource.RelativePath, content));
            packagedFiles.Add(new PackagedFile(
                resource.RelativePath,
                content.Length,
                _hashCalculator.ComputeSha256(content)));
        }

        var archive = await _archiveWriter.CreateAsync(entries, cancellationToken).ConfigureAwait(false);
        var archiveHash = _hashCalculator.ComputeSha256(archive);

        var output = _fileSystem.GetFullPath(outputDirectory);
        _fileSystem.CreateDirectory(output);

        var baseName = $"{skill.Name}.{version}";
        var archivePath = Path.Combine(output, $"{baseName}{ArchiveSuffix}");
        var hashPath = archivePath + HashSuffix;
        var manifestPath = Path.Combine(output, $"{baseName}{ManifestSuffix}");

        var package = new SkillPackage(
            skill.Name,
            version,
            archivePath,
            hashPath,
            manifestPath,
            archiveHash,
            packagedFiles,
            _timeProvider.GetUtcNow());

        await _fileSystem.WriteAllBytesAsync(archivePath, archive, cancellationToken).ConfigureAwait(false);

        // The sha256 file follows the format 'sha256sum -c' expects, so it can be verified with standard tools.
        await _fileSystem.WriteAllTextAsync(
            hashPath,
            $"{archiveHash}  {Path.GetFileName(archivePath)}{Environment.NewLine}",
            cancellationToken).ConfigureAwait(false);

        await _fileSystem.WriteAllTextAsync(
            manifestPath,
            BuildManifest(skill, package),
            cancellationToken).ConfigureAwait(false);

        return OperationResult<SkillPackage>.Success(package);
    }

    private static string BuildManifest(SkillDefinition skill, SkillPackage package)
    {
        var files = new JsonArray();
        foreach (var file in package.Files)
        {
            files.Add(new JsonObject
            {
                ["path"] = file.RelativePath,
                ["sizeInBytes"] = file.SizeInBytes,
                ["sha256"] = file.Sha256,
            });
        }

        var compatibility = new JsonArray();
        foreach (var agent in skill.Frontmatter.Compatibility)
        {
            compatibility.Add(agent);
        }

        var manifest = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["skill"] = new JsonObject
            {
                ["name"] = skill.Name,
                ["description"] = skill.Description,
                ["version"] = package.Version,
                ["license"] = skill.Frontmatter.License,
                ["author"] = skill.Frontmatter.Author,
                ["compatibility"] = compatibility,
            },
            ["package"] = new JsonObject
            {
                ["archive"] = Path.GetFileName(package.ArchivePath),
                ["sha256"] = package.ArchiveSha256,

                // ISO 8601 in UTC: the one field that legitimately differs between builds.
                ["createdAt"] = package.CreatedAtUtc.ToUniversalTime()
                    .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                ["fileCount"] = package.Files.Count,
            },
            ["files"] = files,
        };

        return manifest.ToJsonString(ManifestOptions) + Environment.NewLine;
    }

    private static bool IsExcluded(string relativePath)
    {
        var segments = relativePath.Split('/');

        return segments[..^1].Any(segment =>
                ExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase))
            || ExcludedFiles.Contains(segments[^1], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A package version has to be usable in a file name, so anything with a separator or wildcard is out.
    /// </summary>
    private static bool IsUsableVersion(string version) =>
        version.Length > 0
        && version.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !version.Contains(' ', StringComparison.Ordinal)
        && !version.Contains("..", StringComparison.Ordinal);
}
