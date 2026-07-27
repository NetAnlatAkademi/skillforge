using SkillForge.Application.Diffing;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Validation;

namespace SkillForge.Application.Tests.Diffing;

public sealed class SkillSurfaceDifferTests
{
    [Fact]
    public void IdenticalSkillsHaveNoChanges()
    {
        var diff = SkillSurfaceDiffer.Compare(Snapshot(), Snapshot());

        diff.HasChanges.Should().BeFalse();
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void ANewPermissionMeansTheReachGrew()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(tools: ["filesystem.read"]),
            Snapshot(tools: ["filesystem.read", "shell.execute"]));

        diff.DeclaredTools.Added.Should().Equal("shell.execute");
        diff.DeclaredTools.Removed.Should().BeEmpty();
        diff.ReachGrew.Should().BeTrue();
    }

    [Fact]
    public void ARemovedPermissionIsAChangeButNotGrowth()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(tools: ["filesystem.read", "shell.execute"]),
            Snapshot(tools: ["filesystem.read"]));

        diff.DeclaredTools.Removed.Should().Equal("shell.execute");
        diff.HasChanges.Should().BeTrue();
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void ANewScriptMeansTheReachGrew()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(files: ["SKILL.md"]),
            Snapshot(files: ["SKILL.md", "scripts/run.ps1"]));

        diff.Scripts.Added.Should().Equal("scripts/run.ps1");
        diff.Files.Added.Should().Equal("scripts/run.ps1");
        diff.ReachGrew.Should().BeTrue();
    }

    [Fact]
    public void ANewMarkdownFileIsAChangeButNotGrowth()
    {
        // A document cannot do anything. Only permissions, scripts and hosts widen what a skill reaches.
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(files: ["SKILL.md"]),
            Snapshot(files: ["SKILL.md", "references/notes.md"]));

        diff.Files.Added.Should().Equal("references/notes.md");
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void ANewHostMeansTheReachGrew()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(urls: ["https://learn.microsoft.com/a"]),
            Snapshot(urls: ["https://learn.microsoft.com/a", "https://api.example.com/v1"]));

        diff.ExternalDomains.Added.Should().Equal("api.example.com");
        diff.ReachGrew.Should().BeTrue();
    }

    [Fact]
    public void ADifferentPathOnTheSameHostIsNotAChangeInWhoTheSkillTalksTo()
    {
        // The question a reviewer is asking is "who does this contact", not "which page".
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(urls: ["https://learn.microsoft.com/docs/a"]),
            Snapshot(urls: ["https://learn.microsoft.com/docs/b"]));

        diff.ExternalDomains.HasChanges.Should().BeFalse();
        diff.ReachGrew.Should().BeFalse();
    }

    [Fact]
    public void ReportsNameVersionAndDescriptionChanges()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(name: "before-skill", version: "1.0.0", description: "Use this when reviewing A."),
            Snapshot(name: "after-skill", version: "1.1.0", description: "Use this when reviewing B."));

        diff.Name!.Before.Should().Be("before-skill");
        diff.Name.After.Should().Be("after-skill");
        diff.Version!.After.Should().Be("1.1.0");
        diff.Description!.Before.Should().Be("Use this when reviewing A.");
    }

    [Fact]
    public void UnchangedValuesAreNotReportedAsChanges()
    {
        var diff = SkillSurfaceDiffer.Compare(Snapshot(name: "same"), Snapshot(name: "same"));

        diff.Name.Should().BeNull();
        diff.Version.Should().BeNull();
        diff.Description.Should().BeNull();
    }

    [Fact]
    public void ANewFindingIsReportedAndANewErrorIsSeparable()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(),
            Snapshot(findings:
            [
                Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 12),
                Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "no license", "SKILL.md", 1),
            ]));

        diff.NewFindings.Should().HaveCount(2);
        diff.NewErrors.Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.ReferencedFileNotFound);
        diff.ResolvedFindings.Should().BeEmpty();
    }

    [Fact]
    public void AFindingThatWentAwayIsReportedAsResolved()
    {
        var finding = Diagnostic.Error(DiagnosticCodes.NameMissing, "no name", "SKILL.md", 1);

        var diff = SkillSurfaceDiffer.Compare(Snapshot(findings: [finding]), Snapshot());

        diff.ResolvedFindings.Should().ContainSingle();
        diff.NewFindings.Should().BeEmpty();
    }

    [Fact]
    public void ARewordedDiagnosticAtTheSamePlaceIsNotBothNewAndResolved()
    {
        // Findings are matched by code and location, not message, so improving a message does not read as a
        // finding appearing and another disappearing.
        var before = Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "old wording", "SKILL.md", 1);
        var after = Diagnostic.Warning(DiagnosticCodes.LicenseMissing, "new wording", "SKILL.md", 1);

        var diff = SkillSurfaceDiffer.Compare(Snapshot(findings: [before]), Snapshot(findings: [after]));

        diff.NewFindings.Should().BeEmpty();
        diff.ResolvedFindings.Should().BeEmpty();
    }

    [Fact]
    public void TheSameFindingMovingToAnotherLineIsBothResolvedAndNew()
    {
        var before = Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 10);
        var after = Diagnostic.Error(DiagnosticCodes.ReferencedFileNotFound, "missing", "SKILL.md", 40);

        var diff = SkillSurfaceDiffer.Compare(Snapshot(findings: [before]), Snapshot(findings: [after]));

        diff.NewFindings.Should().ContainSingle();
        diff.ResolvedFindings.Should().ContainSingle();
    }

    [Fact]
    public void SetsAreOrderedSoARunOverUnchangedInputReadsTheSame()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(files: ["SKILL.md"]),
            Snapshot(files: ["SKILL.md", "z.md", "a.md", "m.md"]));

        diff.Files.Added.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsMissingSnapshots()
    {
        var noBefore = () => SkillSurfaceDiffer.Compare(null!, Snapshot());
        var noAfter = () => SkillSurfaceDiffer.Compare(Snapshot(), null!);

        noBefore.Should().Throw<ArgumentNullException>();
        noAfter.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AVersionThatDidNotMoveWhileTheReachGrewIsReported()
    {
        // The `SF6xxx` question: a consumer pinned to 1.0.0 now gets a skill that can run shell commands. The pin
        // did not protect them, and nothing in the version says so.
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(version: "1.0.0", tools: ["filesystem.read"]),
            Snapshot(version: "1.0.0", tools: ["filesystem.read", "shell.execute"]));

        diff.ReachGrew.Should().BeTrue();
        diff.VersionIsSilentAboutGrowth.Should().BeTrue();
    }

    [Fact]
    public void AVersionThatMovedWithTheReachIsNotReported()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(version: "1.0.0", tools: ["filesystem.read"]),
            Snapshot(version: "1.1.0", tools: ["filesystem.read", "shell.execute"]));

        diff.ReachGrew.Should().BeTrue();
        diff.VersionIsSilentAboutGrowth.Should().BeFalse();
    }

    [Fact]
    public void AnUnchangedVersionWithNoGrowthIsNotReported()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(version: "1.0.0", description: "Use this skill when testing the differ carefully."),
            Snapshot(version: "1.0.0", description: "Use this skill when testing the differ thoroughly."));

        diff.HasChanges.Should().BeTrue();
        diff.VersionIsSilentAboutGrowth.Should().BeFalse();
    }

    [Fact]
    public void NoVersionOnEitherSideIsADifferentProblemAndNotThisOne()
    {
        // Nothing was promised, so nothing was broken. "No version is declared" fires on 91% of real skills and is
        // deliberately not a rule; conflating the two would smuggle it in through the back door.
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(version: null, tools: ["filesystem.read"]),
            Snapshot(version: null, tools: ["filesystem.read", "shell.execute"]));

        diff.ReachGrew.Should().BeTrue();
        diff.VersionIsSilentAboutGrowth.Should().BeFalse();
    }

    [Fact]
    public void AVersionAppearingForTheFirstTimeIsNotSilence()
    {
        var diff = SkillSurfaceDiffer.Compare(
            Snapshot(version: null, tools: ["filesystem.read"]),
            Snapshot(version: "1.0.0", tools: ["filesystem.read", "shell.execute"]));

        diff.VersionIsSilentAboutGrowth.Should().BeFalse();
    }

    private static SkillSnapshot Snapshot(
        string name = "demo-skill",
        string? version = "1.0.0",
        string description = "Use this skill when testing the differ.",
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? files = null,
        IReadOnlyList<string>? urls = null,
        IReadOnlyList<Diagnostic>? findings = null)
    {
        var skill = new SkillBuilder()
            .WithName(name)
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
