using System.Globalization;
using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SkillForge.Infrastructure.Yaml;

/// <summary>
/// <see cref="IFrontmatterParser"/> implemented with YamlDotNet.
/// </summary>
/// <remarks>
/// Malformed YAML is an expected input, not an exception: anything the parser rejects becomes
/// <see cref="DiagnosticCodes.FrontmatterNotParsable"/> with the line the parser stopped at. Duplicate
/// top-level fields are found by a deliberate line scan rather than by relying on library behaviour,
/// which lets each repeat be reported with its own line number.
/// </remarks>
public sealed class YamlFrontmatterParser : IFrontmatterParser
{
    private const string NameField = "name";
    private const string DescriptionField = "description";
    private const string LicenseField = "license";
    private const string CompatibilityField = "compatibility";
    private const string AllowedToolsField = "allowed-tools";
    private const string MetadataField = "metadata";
    private const string VersionField = "version";

    /// <inheritdoc />
    public OperationResult<SkillFrontmatter> Parse(string yaml, int startLine, string filePath)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var diagnostics = FindDuplicateFields(yaml, startLine, filePath);

        if (string.IsNullOrWhiteSpace(yaml))
        {
            return OperationResult<SkillFrontmatter>.Success(
                SkillFrontmatter.Empty(startLine, startLine + 1),
                diagnostics);
        }

        YamlMappingNode? root;
        try
        {
            root = LoadRootMapping(yaml);
        }
        catch (YamlException exception)
        {
            // A duplicated field makes YamlDotNet fail too. Reporting SF0003 alongside SF0009 would
            // describe the same mistake twice, so the precise diagnostic wins.
            if (!HasDuplicateFieldError(diagnostics))
            {
                diagnostics.Add(Diagnostic.Error(
                    DiagnosticCodes.FrontmatterNotParsable,
                    $"The YAML frontmatter could not be parsed: {FirstSentenceOf(exception.Message)}",
                    filePath,
                    startLine + (int)exception.Start.Line,
                    "Check indentation and quoting. Values containing ':' must be quoted."));
            }

            return OperationResult<SkillFrontmatter>.Failure(diagnostics);
        }

        if (root is null)
        {
            diagnostics.Add(Diagnostic.Error(
                DiagnosticCodes.FrontmatterNotParsable,
                "The YAML frontmatter is not a set of key/value pairs.",
                filePath,
                startLine + 1,
                "Write the frontmatter as 'field: value' lines, for example 'name: my-skill'."));

            return OperationResult<SkillFrontmatter>.Failure(diagnostics);
        }

        var frontmatter = new SkillFrontmatter(
            Name: ReadScalar(root, NameField),
            Description: ReadScalar(root, DescriptionField),
            License: ReadScalar(root, LicenseField),
            Compatibility: ReadSequence(root, CompatibilityField),
            AllowedTools: ReadSequence(root, AllowedToolsField),
            Metadata: ReadMetadata(root, startLine, filePath, diagnostics),
            StartLine: startLine,
            EndLine: startLine + FrontmatterLineCount(yaml) + 1);

        return OperationResult<SkillFrontmatter>.Success(frontmatter, diagnostics);
    }

    private static bool HasDuplicateFieldError(List<Diagnostic> diagnostics) =>
        diagnostics.Exists(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.DuplicateMetadataField, StringComparison.Ordinal));

    private static YamlMappingNode? LoadRootMapping(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);

        return stream.Documents.Count == 0 ? null : stream.Documents[0].RootNode as YamlMappingNode;
    }

    /// <summary>
    /// Reports every top-level field that appears more than once, in the order the repeats occur.
    /// </summary>
    private static List<Diagnostic> FindDuplicateFields(string yaml, int startLine, string filePath)
    {
        var diagnostics = new List<Diagnostic>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var field = TryReadTopLevelField(lines[index]);
            if (field is null)
            {
                continue;
            }

            var absoluteLine = startLine + index + 1;
            if (seen.TryGetValue(field, out var firstLine))
            {
                diagnostics.Add(Diagnostic.Error(
                    DiagnosticCodes.DuplicateMetadataField,
                    $"The field '{field}' is declared more than once "
                        + $"(first at line {firstLine.ToString(CultureInfo.InvariantCulture)}).",
                    filePath,
                    absoluteLine,
                    $"Remove the repeated '{field}' entry. Only the last one would take effect."));
            }
            else
            {
                seen[field] = absoluteLine;
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Extracts the field name from an unindented <c>field: value</c> line, or returns
    /// <see langword="null"/> when the line is not a top-level field.
    /// </summary>
    private static string? TryReadTopLevelField(string line)
    {
        if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line.StartsWith('#') || line.StartsWith('-'))
        {
            return null;
        }

        var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        var field = line[..separatorIndex].Trim();
        return field.Length == 0 || field.Contains(' ', StringComparison.Ordinal) ? null : field;
    }

    private static string? ReadScalar(YamlMappingNode root, string field) =>
        root.Children.TryGetValue(new YamlScalarNode(field), out var node) && node is YamlScalarNode scalar
            ? NullIfEmpty(scalar.Value)
            : null;

    private static string[] ReadSequence(YamlMappingNode root, string field)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(field), out var node))
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

            // A single value where a list is expected is common enough to accept rather than lose.
            YamlScalarNode scalar when !string.IsNullOrWhiteSpace(scalar.Value) => [scalar.Value!.Trim()],

            _ => [],
        };
    }

    private static Dictionary<string, string> ReadMetadata(
        YamlMappingNode root,
        int startLine,
        string filePath,
        List<Diagnostic> diagnostics)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (root.Children.TryGetValue(new YamlScalarNode(MetadataField), out var node)
            && node is YamlMappingNode mapping)
        {
            foreach (var (key, value) in mapping.Children)
            {
                if (key is YamlScalarNode { Value: { Length: > 0 } name } && value is YamlScalarNode scalar)
                {
                    metadata[name] = scalar.Value ?? string.Empty;
                }
            }
        }

        AdoptTopLevelVersion(root, metadata, startLine, filePath, diagnostics);

        return metadata;
    }

    /// <summary>
    /// Reads a top-level <c>version</c> into the metadata, and reports that it was in the wrong place.
    /// </summary>
    /// <remarks>
    /// The schema keeps the version under <c>metadata</c>, and a <c>version</c> written at the top level used to be
    /// discarded without a word — so SF0010 never checked it, <c>inspect</c>, <c>pack</c> and <c>diff</c> showed no
    /// version, and SF6001 could not fire. It is an easy mistake: every other field a skill declares is top-level.
    ///
    /// Two choices were available and neither alone was right. Accepting it silently would leave the schema
    /// permanently ambiguous; reporting it without reading it would leave the author's value unusable while telling
    /// them off. So SkillForge does both — reads it, and says where it belongs.
    ///
    /// An explicit <c>metadata.version</c> wins. If both are present the author has said the same thing twice, and
    /// the one the schema defines is the one to believe.
    /// </remarks>
    private static void AdoptTopLevelVersion(
        YamlMappingNode root,
        Dictionary<string, string> metadata,
        int startLine,
        string filePath,
        List<Diagnostic> diagnostics)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(VersionField), out var node)
            || node is not YamlScalarNode { Value: { Length: > 0 } version })
        {
            return;
        }

        var alreadyDeclared = metadata.ContainsKey(VersionField);
        if (!alreadyDeclared)
        {
            metadata[VersionField] = version;
        }

        diagnostics.Add(Diagnostic.Warning(
            DiagnosticCodes.VersionOutsideMetadata,
            alreadyDeclared
                ? "A 'version' field is declared at the top level as well as under 'metadata'. "
                    + "The one under 'metadata' is the one being used."
                : "The 'version' field belongs under 'metadata'. It was read from the top level anyway.",
            filePath,
            startLine + (int)node.Start.Line,
            "Move it under 'metadata', which is where the schema looks for it and where every other tool "
                + "reading this skill will look.",
            $"""
             metadata:
               version: {version}
             """));
    }

    private static int FrontmatterLineCount(string yaml) =>
        yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// YamlDotNet messages carry positional detail on later lines; only the first sentence is useful
    /// to someone reading console output.
    /// </summary>
    private static string FirstSentenceOf(string message)
    {
        var firstLine = message.Split('\n', 2)[0].Trim();
        return firstLine.EndsWith('.') ? firstLine : firstLine + ".";
    }
}
