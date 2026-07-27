using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reports references to something remote that can change under the skill's feet.
/// </summary>
/// <remarks>
/// **This rule reads code blocks on purpose, and that is the opposite of what SF4001 does.** The difference is
/// what a code block *is* to each question. To an injection rule a fenced block is an example being displayed, so
/// reading it produces false positives. To a supply-chain rule it is the install command the agent will actually
/// run, so skipping it produces false negatives. Same construct, opposite treatment, because the questions differ.
///
/// Scripts are read as well, through <see cref="IFileSystem"/>, and an unreadable one is skipped rather than
/// reported — "there might be something in here" is noise, and an unreadable file in one's own repository is a
/// different problem.
/// </remarks>
public sealed class MutableRemoteReferenceRule : ISkillValidationRule
{
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the rule.</summary>
    /// <param name="fileSystem">Used to read the scripts the skill ships.</param>
    public MutableRemoteReferenceRule(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public string Code => DiagnosticCodes.MutableRemoteReference;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var diagnostics = new List<Diagnostic>();

        if (!string.IsNullOrEmpty(skill.Body))
        {
            diagnostics.AddRange(Scan(
                SkillDefinition.SkillFileName,
                skill.Body,
                firstLineNumber: skill.BodyStartLine));
        }

        foreach (var script in skill.Resources.Where(r => r.Kind == SkillResourceKind.Script))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await TryReadAsync(script.AbsolutePath, cancellationToken).ConfigureAwait(false);
            if (content is not null)
            {
                diagnostics.AddRange(Scan(script.RelativePath, content, firstLineNumber: 1));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Reports each pattern at most once per file, at the first line it appears on.
    /// </summary>
    private IEnumerable<Diagnostic> Scan(string file, string content, int firstLineNumber)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var pattern in MutableReferencePatterns.All)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (!pattern.Pattern.IsMatch(lines[index]))
                {
                    continue;
                }

                yield return Diagnostic.Warning(
                    Code,
                    $"{file} contains {pattern.Name}.",
                    file,
                    firstLineNumber + index,
                    $"Decide this deliberately: {pattern.Why}. SkillForge is pointing it out, not calling it "
                        + "unsafe.");

                break;
            }
        }
    }

    private async Task<string?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or KeyNotFoundException)
        {
            return null;
        }
    }
}
