using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SkillForge.Infrastructure.Yaml;

/// <summary>
/// Reads <c>skillforge.yaml</c> with YamlDotNet.
/// </summary>
/// <remarks>
/// Only the <c>validation</c> section is read today. The rest of the file — permissions, package options — is
/// declared but not yet enforced, and <c>docs/skillforge-manifest-rfc.md</c> says so rather than implying the
/// declarations do something.
///
/// An unreadable or malformed file yields the defaults plus SF1012. Failing the whole run would punish the user
/// for a typo in an optional file; ignoring it silently would let a suppression they wrote quietly not apply.
/// </remarks>
public sealed class YamlSkillConfigurationReader : ISkillConfigurationReader
{
    private const string ValidationSection = "validation";
    private const string StrictField = "strict";
    private const string SuppressField = "suppress";

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the reader.</summary>
    /// <param name="fileSystem">File system used to read the file.</param>
    public YamlSkillConfigurationReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<OperationResult<SkillConfiguration>> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);

        var path = Path.Combine(skillDirectory, SkillDefinition.ConfigurationFileName);
        if (!_fileSystem.FileExists(path))
        {
            return OperationResult<SkillConfiguration>.Success(SkillConfiguration.Default);
        }

        string content;
        try
        {
            content = await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Ignored($"it could not be read: {exception.Message}");
        }

        YamlMappingNode? root;
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(content);
            stream.Load(reader);

            root = stream.Documents.Count == 0 ? null : stream.Documents[0].RootNode as YamlMappingNode;
        }
        catch (YamlException exception)
        {
            return Ignored(FirstSentenceOf(exception.Message));
        }

        if (root is null)
        {
            // An empty file is a file with nothing to say, not a broken one.
            return OperationResult<SkillConfiguration>.Success(SkillConfiguration.Default);
        }

        if (!root.Children.TryGetValue(new YamlScalarNode(ValidationSection), out var section)
            || section is not YamlMappingNode validation)
        {
            return OperationResult<SkillConfiguration>.Success(SkillConfiguration.Default);
        }

        return OperationResult<SkillConfiguration>.Success(new SkillConfiguration(
            ReadBoolean(validation, StrictField),
            ReadCodes(validation, SuppressField)));
    }

    private static OperationResult<SkillConfiguration> Ignored(string reason) =>
        OperationResult<SkillConfiguration>.Success(
            SkillConfiguration.Default,
            [
                Diagnostic.Warning(
                    DiagnosticCodes.ConfigurationNotParsable,
                    $"{SkillDefinition.ConfigurationFileName} was ignored because {reason}",
                    SkillDefinition.ConfigurationFileName,
                    suggestion: "Fix the file or remove it. Its settings — including any suppressions — are "
                        + "not being applied."),
            ]);

    private static bool ReadBoolean(YamlMappingNode section, string field) =>
        section.Children.TryGetValue(new YamlScalarNode(field), out var node)
        && node is YamlScalarNode { Value: { Length: > 0 } value }
        && bool.TryParse(value, out var parsed)
        && parsed;

    private static string[] ReadCodes(YamlMappingNode section, string field)
    {
        if (!section.Children.TryGetValue(new YamlScalarNode(field), out var node))
        {
            return [];
        }

        return node switch
        {
            YamlSequenceNode sequence => sequence.Children
                .OfType<YamlScalarNode>()
                .Select(static item => item.Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim())
                .ToArray(),

            // A single code where a list is expected is common enough to accept rather than lose.
            YamlScalarNode { Value: { Length: > 0 } single } => [single.Trim()],

            _ => [],
        };
    }

    private static string FirstSentenceOf(string message)
    {
        var firstLine = message.Split('\n', 2)[0].Trim();
        return firstLine.EndsWith('.') ? firstLine : firstLine + ".";
    }
}
