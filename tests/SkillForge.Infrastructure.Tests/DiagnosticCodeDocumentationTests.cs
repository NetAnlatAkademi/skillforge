using System.Reflection;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Infrastructure.Tests;

/// <summary>
/// Every diagnostic code the tool can emit is documented.
/// </summary>
/// <remarks>
/// This replaces the count-based test that used to guard the code set. A code's whole value is that someone can
/// look it up and decide whether to suppress it, so an undocumented code is a broken promise — and far easier to
/// add by accident than a duplicate. This test lives here rather than in the Domain tests because it reads a file
/// from the repository, which is what this project already has the machinery for.
/// </remarks>
public sealed class DiagnosticCodeDocumentationTests
{
    private static readonly string RulesDocument =
        File.ReadAllText(Path.Combine(RepositoryPaths.RepositoryRoot, "docs", "validation-rules.md"));

    private static readonly IReadOnlyList<string> AllCodes = typeof(DiagnosticCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false })
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void EveryDeclaredCodeAppearsInTheRulesDocument()
    {
        var undocumented = AllCodes
            .Where(code => !RulesDocument.Contains(code, StringComparison.Ordinal))
            .ToArray();

        undocumented.Should().BeEmpty(
            "docs/validation-rules.md is where a reader looks a code up before deciding to suppress it");
    }

    [Fact]
    public void TheDocumentDoesNotDescribeCodesThatDoNotExist()
    {
        // Catches the other direction: a code renamed in the source but left behind in the docs.
        var documented = System.Text.RegularExpressions.Regex
            .Matches(RulesDocument, @"\bSF[0-6]\d{3}\b")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        documented.Should().OnlyContain(code => AllCodes.Contains(code, StringComparer.Ordinal));
    }
}
