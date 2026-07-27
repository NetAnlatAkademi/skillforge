using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class LicenseDeclaredRuleTests
{
    private readonly LicenseDeclaredRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.LicenseMissing);

    [Fact]
    public async Task SaysNothingWhenALicenseIsDeclared()
    {
        (await _rule.Run(new SkillBuilder().Build())).Should().BeEmpty();
    }

    [Fact]
    public async Task WarnsWhenNoLicenseIsDeclared()
    {
        var diagnostics = await _rule.Run(new SkillBuilder().WithLicense(null).Build());

        diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
