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
/// The <c>validation</c> and <c>permissions</c> sections are read. Package options are still declared but not
/// enforced, and <c>docs/skillforge-manifest-rfc.md</c> says so rather than implying otherwise.
///
/// An unreadable or malformed file yields the defaults plus SF1012. Failing the whole run would punish the user
/// for a typo in an optional file; ignoring it silently would let a suppression they wrote quietly not apply.
/// </remarks>
public sealed class YamlSkillConfigurationReader : ISkillConfigurationReader
{
    private const string ValidationSection = "validation";
    private const string StrictField = "strict";
    private const string SuppressField = "suppress";
    private const string PermissionsSection = "permissions";
    private const string NetworkSection = "network";
    private const string ShellSection = "shell";
    private const string AllowedField = "allowed";

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
            // An empty file is a file with nothing to say, not a broken one — but it does exist, and a rule may
            // care about the difference between "declared nothing" and "shipped no file".
            return OperationResult<SkillConfiguration>.Success(
                SkillConfiguration.Default with { Exists = true });
        }

        var validation = Section(root, ValidationSection);
        var permissions = Section(root, PermissionsSection);

        var configuration = new SkillConfiguration(
            validation is null ? false : ReadBoolean(validation, StrictField),
            validation is null ? [] : ReadCodes(validation, SuppressField))
        {
            Exists = true,
            NetworkAllowed = ReadNullableBoolean(Section(permissions, NetworkSection), AllowedField),
            ShellAllowed = ReadStrings(Section(permissions, ShellSection), AllowedField),
        };

        return OperationResult<SkillConfiguration>.Success(configuration);
    }

    private static YamlMappingNode? Section(YamlMappingNode? parent, string name) =>
        parent is not null
        && parent.Children.TryGetValue(new YamlScalarNode(name), out var node)
        && node is YamlMappingNode section
            ? section
            : null;

    /// <summary>
    /// Reads a boolean that may be absent, because "declared false" and "said nothing" are different claims.
    /// </summary>
    private static bool? ReadNullableBoolean(YamlMappingNode? section, string field)
    {
        if (section is null
            || !section.Children.TryGetValue(new YamlScalarNode(field), out var node)
            || node is not YamlScalarNode { Value: { Length: > 0 } value })
        {
            return null;
        }

        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string[] ReadStrings(YamlMappingNode? section, string field) =>
        section is null ? [] : ReadCodes(section, field);

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
