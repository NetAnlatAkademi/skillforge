using SkillForge.Domain;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Tests;

public sealed class OperationResultTests
{
    [Fact]
    public void SuccessCarriesTheValue()
    {
        var result = OperationResult<string>.Success("loaded");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("loaded");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void SuccessMayStillCarryDiagnostics()
    {
        // A skill can load correctly and still be worth warning about.
        var warning = Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license");

        var result = OperationResult<string>.Success("loaded", [warning]);

        result.IsSuccess.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle().Which.Should().Be(warning);
    }

    [Fact]
    public void FailureHasNoValue()
    {
        var error = Diagnostic.Error(DiagnosticCodes.SkillFileNotFound, "not found");

        var result = OperationResult<string>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Diagnostics.Should().ContainSingle().Which.Should().Be(error);
    }

    [Fact]
    public void FailureAcceptsSeveralDiagnostics()
    {
        Diagnostic[] diagnostics =
        [
            Diagnostic.Error(DiagnosticCodes.DuplicateMetadataField, "duplicate"),
            Diagnostic.Error(DiagnosticCodes.FrontmatterNotParsable, "unparsable"),
        ];

        OperationResult<string>.Failure(diagnostics)
            .Diagnostics.Should().HaveCount(2);
    }
}
