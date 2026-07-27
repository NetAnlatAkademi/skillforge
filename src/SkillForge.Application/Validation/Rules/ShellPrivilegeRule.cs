using SkillForge.Application.Abstractions;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Validation.Rules;

/// <summary>
/// Reads the skill's scripts and points out constructs that reach further than a script usually needs to.
/// </summary>
/// <remarks>
/// The only rule that opens a file. It still goes through <see cref="IFileSystem"/>, so it is as testable as any
/// other rule — but it is worth noticing that this is where validation stops being a pure function of the loaded
/// model, because the alternative was putting every script's text into that model for one rule's benefit.
///
/// These are signals, not verdicts (ADR-006). Each pattern has legitimate uses; the point is that a reader
/// deciding whether to trust a skill would want to know they are there, and nobody reads every script by hand.
///
/// A script that cannot be read is skipped rather than reported. Saying "there might be something in here" would
/// add noise without adding information, and an unreadable file in one's own repository is a different problem.
/// </remarks>
public sealed class ShellPrivilegeRule : ISkillValidationRule
{
    private readonly IFileSystem _fileSystem;

    /// <summary>Initialises the rule.</summary>
    /// <param name="fileSystem">Used to read the scripts the skill ships.</param>
    public ShellPrivilegeRule(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public string Code => DiagnosticCodes.BroadShellPrivileges;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Diagnostic>> ValidateAsync(
        SkillDefinition skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var scripts = skill.Resources
            .Where(resource => resource.Kind == SkillResourceKind.Script)
            .ToArray();

        if (scripts.Length == 0)
        {
            return [];
        }

        var diagnostics = new List<Diagnostic>();

        foreach (var script in scripts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await TryReadAsync(script.AbsolutePath, cancellationToken).ConfigureAwait(false);
            if (content is null)
            {
                continue;
            }

            diagnostics.AddRange(Scan(script.RelativePath, content));
        }

        return diagnostics;
    }

    private IEnumerable<Diagnostic> Scan(string relativePath, string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        // One finding per pattern per file, at its first appearance: a loop that writes the same warning for every
        // line of a long script buries everything else in the report.
        foreach (var pattern in ShellPrivilegePatterns.All)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (!pattern.Pattern.IsMatch(lines[index]))
                {
                    continue;
                }

                yield return Diagnostic.Warning(
                    Code,
                    $"{relativePath} uses {pattern.Name}.",
                    relativePath,
                    index + 1,
                    $"Check this deliberately: {pattern.Why}. SkillForge is pointing it out, not calling it "
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
