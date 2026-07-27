using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class PackageVersionRuleTests
{
    private readonly PackageVersionRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.PackageVersionInvalid);

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.1.0")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0+build.5")]
    public async Task AcceptsSemanticVersions(string version)
    {
        (await _rule.Run(new SkillBuilder().WithVersion(version).Build())).Should().BeEmpty();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("latest")]
    [InlineData("1.0.0.0")]
    public async Task RejectsAnythingElse(string version)
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithVersion(version).Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SaysNothingWhenNoVersionIsDeclared()
    {
        // A version is optional in the first release; only a malformed one is an error.
        (await _rule.Run(new SkillBuilder().WithVersion(null).Build())).Should().BeEmpty();
    }
}
