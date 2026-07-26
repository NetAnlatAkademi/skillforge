using SkillForge.Application.Skills;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Validation;

/// <summary>
/// Builds <see cref="SkillDefinition"/> instances for rule tests.
/// </summary>
/// <remarks>
/// Defaults describe a skill that passes every rule, so each test changes exactly the one thing it is
/// about. That keeps a test's intent visible and stops an unrelated rule from being the reason a test
/// goes red.
/// </remarks>
internal sealed class SkillBuilder
{
    private const string DefaultDirectory = "/skills/demo";

    private string _name = "demo-skill";
    private string _description =
        "Use this skill when reviewing a demo project for correctness and clarity.";
    private string? _license = "MIT";
    private IReadOnlyList<string> _compatibility = ["claude-code"];
    private IReadOnlyList<string> _allowedTools = [];
    private Dictionary<string, string> _metadata = new(StringComparer.Ordinal) { ["version"] = "1.0.0" };
    private string _body = "# Demo\n\nNothing to see here.";
    private int _bodyStartLine = 6;
    private int _lineCount = 8;
    private List<SkillResource> _resources =
    [
        new("SKILL.md", "/skills/demo/SKILL.md", SkillResourceKind.SkillDocument, 100),
    ];

    internal SkillBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    internal SkillBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    internal SkillBuilder WithLicense(string? license)
    {
        _license = license;
        return this;
    }

    internal SkillBuilder WithCompatibility(params string[] agents)
    {
        _compatibility = agents;
        return this;
    }

    internal SkillBuilder WithAllowedTools(params string[] tools)
    {
        _allowedTools = tools;
        return this;
    }

    internal SkillBuilder WithVersion(string? version)
    {
        _metadata = version is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = version };
        return this;
    }

    internal SkillBuilder WithBody(string body, int bodyStartLine = 6)
    {
        _body = body;
        _bodyStartLine = bodyStartLine;
        return this;
    }

    internal SkillBuilder WithSkillFileLineCount(int lineCount)
    {
        _lineCount = lineCount;
        return this;
    }

    internal SkillBuilder WithResources(params string[] relativePaths)
    {
        _resources = relativePaths
            .Select(path => new SkillResource(
                path,
                $"{DefaultDirectory}/{path}",
                SkillResourceClassifier.Classify(path),
                10))
            .ToList();
        return this;
    }

    internal SkillDefinition Build()
    {
        var frontmatter = new SkillFrontmatter(
            _name.Length == 0 ? null : _name,
            _description.Length == 0 ? null : _description,
            _license,
            _compatibility,
            _allowedTools,
            _metadata,
            1,
            5);

        return new SkillDefinition(
            _name,
            _description,
            DefaultDirectory,
            $"{DefaultDirectory}/SKILL.md",
            frontmatter,
            _resources,
            _body,
            _bodyStartLine,
            _lineCount);
    }
}
