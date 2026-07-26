using System.Reflection;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Tests.Diagnostics;

public sealed class DiagnosticCodesTests
{
    private static readonly IReadOnlyList<string> AllCodes = typeof(DiagnosticCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false })
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void EveryCodeIsUnique()
    {
        // A reused code would silently change the meaning of a rule for anyone suppressing it.
        AllCodes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryCodeFollowsTheNamingScheme()
    {
        AllCodes.Should().AllSatisfy(code =>
        {
            code.Should().MatchRegex("^SF[0-2][0-9]{3}$");
        });
    }

    [Fact]
    public void TheRoadmapsTwentyFourCodesAreAllDeclared()
    {
        AllCodes.Should().HaveCount(24);
    }

    [Theory]
    [InlineData("SF0", 10)]
    [InlineData("SF1", 10)]
    [InlineData("SF2", 4)]
    public void CodesAreGroupedBySeverityBand(string prefix, int expectedCount)
    {
        AllCodes.Count(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .Should().Be(expectedCount);
    }
}
