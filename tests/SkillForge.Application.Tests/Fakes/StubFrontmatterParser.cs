using SkillForge.Application.Abstractions;
using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// <see cref="IFrontmatterParser"/> stub that returns whatever the test asks for.
/// </summary>
/// <remarks>
/// Keeps loader tests about the loader. Real YAML behaviour is covered by the Infrastructure tests.
/// </remarks>
internal sealed class StubFrontmatterParser : IFrontmatterParser
{
    private readonly OperationResult<SkillFrontmatter> _result;

    private StubFrontmatterParser(OperationResult<SkillFrontmatter> result) => _result = result;

    /// <summary>The YAML text the loader passed in on the last call.</summary>
    internal string? ReceivedYaml { get; private set; }

    /// <summary>The start line the loader passed in on the last call.</summary>
    internal int ReceivedStartLine { get; private set; }

    internal static StubFrontmatterParser Returning(
        string? name = "sample-skill",
        string? description = "Use this skill when testing the loader.",
        string? license = "MIT",
        IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        var frontmatter = new SkillFrontmatter(
            name,
            description,
            license,
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal),
            1,
            2);

        return new StubFrontmatterParser(OperationResult<SkillFrontmatter>.Success(frontmatter, diagnostics));
    }

    internal static StubFrontmatterParser Failing(string code = DiagnosticCodes.FrontmatterNotParsable) =>
        new(OperationResult<SkillFrontmatter>.Failure(Diagnostic.Error(code, "stub parse failure")));

    public OperationResult<SkillFrontmatter> Parse(string yaml, int startLine, string filePath)
    {
        ReceivedYaml = yaml;
        ReceivedStartLine = startLine;
        return _result;
    }
}
