using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Evaluation;
using YamlDotNet.RepresentationModel;

namespace SkillForge.Infrastructure.Yaml;

/// <summary>
/// Reads a skill's eval cases from <c>evals/*.yaml</c>.
/// </summary>
/// <remarks>
/// One file or many, all merged in file-name order so a run is reproducible. A file that cannot be parsed is reported
/// and skipped rather than failing the run: the same choice SF1012 makes for <c>skillforge.yaml</c>, for the same
/// reason — an unreadable file is the author's problem to see, not a reason to refuse to evaluate the rest.
/// </remarks>
public sealed class YamlEvalCaseReader : IEvalCaseReader
{
    private const string EvalsDirectory = "evals";

    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the reader.</summary>
    /// <param name="fileSystem">Used to find and read the eval files.</param>
    public YamlEvalCaseReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<EvalCase>>> ReadAsync(
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);

        var directory = Path.Combine(skillDirectory, EvalsDirectory);
        if (!_fileSystem.DirectoryExists(directory))
        {
            return OperationResult<IReadOnlyList<EvalCase>>.Success([]);
        }

        var files = _fileSystem.EnumerateFiles(directory)
            .Where(path => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var cases = new List<EvalCase>();
        var diagnostics = new List<Diagnostic>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = $"{EvalsDirectory}/{Path.GetFileName(file)}";

            string content;
            try
            {
                content = await _fileSystem.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(Diagnostic.Warning(
                    DiagnosticCodes.EvalFileNotParsable,
                    $"{relative} could not be read, so its cases were skipped.",
                    relative));
                continue;
            }

            if (TryReadCases(content, out var fileCases, out var reason))
            {
                cases.AddRange(fileCases);
            }
            else
            {
                diagnostics.Add(Diagnostic.Warning(
                    DiagnosticCodes.EvalFileNotParsable,
                    $"{relative} could not be parsed, so its cases were skipped: {reason}",
                    relative,
                    suggestion: "Each file needs a 'cases:' list, and each case a 'name'."));
            }
        }

        return OperationResult<IReadOnlyList<EvalCase>>.Success(cases, diagnostics);
    }

    private static bool TryReadCases(
        string content,
        out IReadOnlyList<EvalCase> cases,
        out string reason)
    {
        cases = [];
        reason = string.Empty;

        YamlMappingNode? root;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(content));
            root = stream.Documents.Count == 0 ? null : stream.Documents[0].RootNode as YamlMappingNode;
        }
        catch (YamlDotNet.Core.YamlException exception)
        {
            reason = exception.Message;
            return false;
        }

        if (root is null)
        {
            reason = "it is not a set of key/value pairs";
            return false;
        }

        if (!root.Children.TryGetValue(new YamlScalarNode("cases"), out var node)
            || node is not YamlSequenceNode sequence)
        {
            reason = "there is no 'cases' list";
            return false;
        }

        var read = new List<EvalCase>();
        foreach (var entry in sequence.Children.OfType<YamlMappingNode>())
        {
            var name = Scalar(entry, "name");
            if (name is null)
            {
                reason = "a case has no 'name'";
                return false;
            }

            read.Add(ReadCase(entry, name));
        }

        cases = read;
        return true;
    }

    private static EvalCase ReadCase(YamlMappingNode entry, string name)
    {
        var activation = entry.Children.TryGetValue(new YamlScalarNode("activation"), out var node)
            && node is YamlMappingNode activationNode
                ? ReadActivation(activationNode)
                : null;

        return new EvalCase(
            name,
            Sequence(entry, "files"),
            Boolean(entry, "shell"),
            Sequence(entry, "forbid"),
            Sequence(entry, "expect"),
            Sequence(entry, "mentions"),
            activation);
    }

    /// <summary>
    /// Reads an activation case. <c>overlap</c> defaults to <see langword="true"/>, since a case naming a prompt
    /// without saying anything else is asking whether the skill's wording reaches it.
    /// </summary>
    private static ActivationExpectation? ReadActivation(YamlMappingNode node)
    {
        var prompt = Scalar(node, "prompt");

        return prompt is null ? null : new ActivationExpectation(prompt, Boolean(node, "overlap") ?? true);
    }

    private static string? Scalar(YamlMappingNode node, string field) =>
        node.Children.TryGetValue(new YamlScalarNode(field), out var value)
            && value is YamlScalarNode { Value: { Length: > 0 } text }
                ? text
                : null;

    private static bool? Boolean(YamlMappingNode node, string field) =>
        Scalar(node, field) switch
        {
            null => null,
            var text when bool.TryParse(text, out var parsed) => parsed,
            "required" or "yes" => true,
            "forbidden" or "no" => false,
            _ => null,
        };

    private static IReadOnlyList<string> Sequence(YamlMappingNode node, string field) =>
        node.Children.TryGetValue(new YamlScalarNode(field), out var value) && value is YamlSequenceNode sequence
            ? [.. sequence.Children.OfType<YamlScalarNode>()
                .Select(scalar => scalar.Value)
                .OfType<string>()
                .Where(text => text.Length > 0)]
            : [];
}
