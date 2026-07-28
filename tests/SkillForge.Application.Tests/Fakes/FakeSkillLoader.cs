using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// <see cref="ISkillLoader"/> stub that answers per directory.
/// </summary>
/// <remarks>
/// The migration tests need different skills in different directories, which the frontmatter stub cannot express
/// because it answers the same thing every time. A directory this was not told about fails to load, which is the
/// case the inventory has to survive.
/// </remarks>
internal sealed class FakeSkillLoader : ISkillLoader
{
    private readonly Dictionary<string, SkillDefinition> _skills = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a skill that loads from <paramref name="directory"/>.</summary>
    internal FakeSkillLoader WithSkill(
        string directory,
        string name,
        params string[] compatibility)
    {
        var frontmatter = new SkillFrontmatter(
            name,
            "Use this skill when testing the migration inventory.",
            "MIT",
            compatibility,
            [],
            new Dictionary<string, string>(StringComparer.Ordinal),
            1,
            4);

        _skills[Normalise(directory)] = new SkillDefinition(
            name,
            frontmatter.Description!,
            directory,
            $"{directory}/SKILL.md",
            frontmatter,
            [],
            $"# {name}",
            BodyStartLine: 6,
            SkillFileLineCount: 8);

        return this;
    }

    public Task<OperationResult<SkillDefinition>> LoadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(_skills.TryGetValue(Normalise(path), out var skill)
            ? OperationResult<SkillDefinition>.Success(skill)
            : OperationResult<SkillDefinition>.Failure(
                Diagnostic.Error(DiagnosticCodes.FrontmatterNotParsable, "stub load failure")));

    private static string Normalise(string path) => path.Replace('\\', '/').TrimEnd('/');
}
