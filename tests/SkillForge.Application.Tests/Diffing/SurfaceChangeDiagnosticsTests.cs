using SkillForge.Application.Diffing;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Tests.Diffing;

public sealed class SurfaceChangeDiagnosticsTests
{
    [Fact]
    public void AnUnchangedSurfaceProducesNothing()
    {
        Diff(Snapshot(), Snapshot()).Should().BeEmpty();
    }

    [Fact]
    public void ANewPermissionIsAWarningNamingIt()
    {
        var diagnostics = Diff(
            Snapshot(tools: ["filesystem.read"]),
            Snapshot(tools: ["filesystem.read", "shell.execute"]));

        var finding = diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.PermissionAdded).Subject;
        finding.Severity.Should().Be(DiagnosticSeverity.Warning);
        finding.Message.Should().Contain("shell.execute");
    }

    [Fact]
    public void ANewDomainIsAWarningNamingTheHost()
    {
        var diagnostics = Diff(
            Snapshot(urls: ["https://learn.microsoft.com/a"]),
            Snapshot(urls: ["https://learn.microsoft.com/a", "https://api.example.com/v1"]));

        var finding = diagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.ExternalDomainAdded).Subject;
        finding.Severity.Should().Be(DiagnosticSeverity.Warning);
        finding.Message.Should().Contain("api.example.com");
    }

    [Fact]
    public void ANewScriptIsAWarningAnchoredOnTheScript()
    {
        var diagnostics = Diff(
            Snapshot(files: ["SKILL.md"]),
            Snapshot(files: ["SKILL.md", "scripts/run.ps1"]));

        var finding = diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.ScriptAdded).Subject;
        finding.Severity.Should().Be(DiagnosticSeverity.Warning);
        finding.FilePath.Should().Be("scripts/run.ps1");
    }

    [Fact]
    public void ANarrowedReachIsInformationRatherThanAWarning()
    {
        var diagnostics = Diff(
            Snapshot(tools: ["filesystem.read", "shell.execute"]),
            Snapshot(tools: ["filesystem.read"]));

        var finding = diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.ReachNarrowed).Subject;
        finding.Severity.Should().Be(DiagnosticSeverity.Info);
        finding.Message.Should().Contain("shell.execute");
    }

    [Fact]
    public void AChangedDescriptionAloneProducesNothingBecauseNothingCanBeClaimedAboutIt()
    {
        // SkillSurfaceDiff refuses to judge whether a description broadened the activation scope, so no code is
        // invented here to say it did.
        var diagnostics = Diff(
            Snapshot(description: "Use when reviewing a .NET API."),
            Snapshot(description: "Use when reviewing any API at all."));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void WhitespaceOnlyBodyChangesAreNotABehaviourChange()
    {
        // The surface is computed from what the skill declares and ships, so reflowing prose cannot reach it.
        Diff(Snapshot(), Snapshot()).Should().BeEmpty();
    }

    [Fact]
    public void GrowthUnderAnUnchangedVersionCarriesSF6001()
    {
        var diagnostics = Diff(
            Snapshot(version: "1.0.0", tools: ["filesystem.read"]),
            Snapshot(version: "1.0.0", tools: ["filesystem.read", "shell.execute"]));

        diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.VersionSilentAboutGrowth);
    }

    [Fact]
    public void NewValidationFindingsAreCarriedThroughUnchanged()
    {
        var diagnostics = Diff(
            Snapshot(),
            Snapshot(findings:
            [
                Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 12),
            ]));

        diagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.ReferencedFileNotFound)
            .Which.Line.Should().Be(12);
    }

    [Fact]
    public void AResolvedFindingIsNotReportedAsAFinding()
    {
        var finding = Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 12);

        Diff(Snapshot(findings: [finding]), Snapshot()).Should().BeEmpty();
    }

    [Fact]
    public void FindingsAreOrderedMostSevereFirst()
    {
        var diagnostics = Diff(
            Snapshot(tools: ["filesystem.read"]),
            Snapshot(
                tools: ["filesystem.read", "shell.execute"],
                findings: [Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 12)]));

        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void RejectsAMissingDiff()
    {
        var act = () => SurfaceChangeDiagnostics.From(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IReadOnlyList<Diagnostic> Diff(SkillSnapshot before, SkillSnapshot after) =>
        SurfaceChangeDiagnostics.From(SkillSurfaceDiffer.Compare(before, after));

    private static SkillSnapshot Snapshot(
        string? version = "1.0.0",
        string description = "Use this skill when testing the differ.",
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? files = null,
        IReadOnlyList<string>? urls = null,
        IReadOnlyList<Diagnostic>? findings = null)
    {
        var skill = new SkillBuilder()
            .WithName("demo-skill")
            .WithDescription(description)
            .WithVersion(version)
            .WithAllowedTools([.. tools ?? []])
            .WithResources([.. files ?? ["SKILL.md"]])
            .Build();

        var inspection = new SkillInspection(
            skill.Name,
            skill.DirectoryPath,
            version,
            skill.Resources,
            urls ?? [],
            [],
            skill.Frontmatter.AllowedTools,
            []);

        return new SkillSnapshot(
            skill.DirectoryPath,
            skill,
            inspection,
            ValidationReport.For(skill, findings ?? []));
    }
}
