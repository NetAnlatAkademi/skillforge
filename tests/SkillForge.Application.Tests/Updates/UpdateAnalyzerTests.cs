using SkillForge.Application.Updates;
using SkillForge.Domain.Diffing;
using SkillForge.Domain.Inspection;
using SkillForge.Domain.Provenance;
using SkillForge.Domain.Updates;

namespace SkillForge.Application.Tests.Updates;

/// <summary>
/// The risk matrix, which is the whole opinion this command has.
/// </summary>
/// <remarks>
/// Each test is one row of it. The pair that matters most is the first two: the same new script is medium when
/// somebody chose to install it and high when it arrives on its own, and a tool that scored them the same would be
/// telling a reviewer that the review they did not get made no difference.
/// </remarks>
public sealed class UpdateAnalyzerTests
{
    [Fact]
    public void APinnedUpdateThatAddsAScriptIsMedium()
    {
        var analysis = Analyze(UpdateMode.Pinned, scripts: ["install.sh"]);

        analysis.Risk.Should().Be(UpdateRisk.Medium);
    }

    [Fact]
    public void AnAutomaticUpdateThatAddsAScriptIsHigh()
    {
        var analysis = Analyze(UpdateMode.Automatic, scripts: ["install.sh"]);

        analysis.Risk.Should().Be(UpdateRisk.High);
        analysis.Findings.Should().Contain(finding => finding.Code == "SF5303");
    }

    [Fact]
    public void AnAutomaticUpdateThatReadsANewCredentialIsHigh()
    {
        var analysis = Analyze(UpdateMode.Automatic, credentials: ["DEPLOY_TOKEN"]);

        analysis.Risk.Should().Be(UpdateRisk.High);
    }

    [Fact]
    public void AFloatingUpdateThatReachesANewHostIsHigh()
    {
        var analysis = Analyze(UpdateMode.Floating, domains: ["prod.company.com"]);

        analysis.Risk.Should().Be(UpdateRisk.High);
    }

    [Fact]
    public void APublisherChangeUnderAnAutomaticUpdateIsCritical()
    {
        var analysis = Analyze(UpdateMode.Automatic, publisher: ("company", "someone-else"));

        analysis.Risk.Should().Be(UpdateRisk.Critical);
        analysis.Reason.Should().Contain("publisher");
    }

    [Fact]
    public void APublisherChangeOnItsOwnIsStillHigh()
    {
        // Nothing arrives unreviewed yet, but every future update comes from somebody else.
        var analysis = Analyze(UpdateMode.Pinned, publisher: ("company", "someone-else"));

        analysis.Risk.Should().Be(UpdateRisk.High);
    }

    [Fact]
    public void APinnedUpdateThatChangesNothingReachableIsInformational()
    {
        var analysis = Analyze(UpdateMode.Pinned);

        analysis.Risk.Should().Be(UpdateRisk.Informational);
        analysis.Findings.Should().BeEmpty();
    }

    [Fact]
    public void AnUpdateThatOnlyAddsAReferenceFileIsNotAnExpansion()
    {
        // Content changed and nothing new runs, reaches or reads. Reporting that as growth is how a report about
        // reach starts warning about documentation.
        var analysis = Analyze(UpdateMode.Automatic, skills: ["notes"]);

        analysis.Findings.Should().NotContain(finding => finding.Code == "SF5303");
    }

    [Fact]
    public void AnAutomaticUpdateThatGivesSomethingUpIsNotAnExpansion()
    {
        var capabilities = new CapabilitySurfaceDiff(
            SurfaceSetDiff.Unchanged,
            SurfaceSetDiff.Unchanged,
            SurfaceSetDiff.Unchanged,
            new SurfaceSetDiff([], ["install.sh"]),
            SurfaceSetDiff.Unchanged,
            SurfaceSetDiff.Unchanged,
            new SurfaceSetDiff([], [SkillCapabilities.ShellExecution]));

        var analysis = UpdateAnalyzer.Analyze("/base", "/target", UpdateMode.Automatic, NoDrift, capabilities);

        analysis.Risk.Should().Be(UpdateRisk.Informational);
        analysis.Findings.Should().BeEmpty();
    }

    [Fact]
    public void TheModeOfATreeIsTheMostPermissiveOneInIt()
    {
        var report = new ProvenanceReport(
            "/tree",
            null,
            null,
            [Asset("pinned", UpdateMode.Pinned), Asset("floating", UpdateMode.Floating)],
            []);

        UpdateAnalyzer.ModeOf(report).Should().Be(UpdateMode.Floating);
    }

    [Fact]
    public void ATreeNothingSaysAnythingAboutHasAnUnknownMode()
    {
        var report = new ProvenanceReport("/tree", null, null, [Asset("plain", UpdateMode.Unknown)], []);

        UpdateAnalyzer.ModeOf(report).Should().Be(UpdateMode.Unknown);
    }

    [Fact]
    public void APublisherChangeIsReportedOnceEvenWhenEverySkillInheritedIt()
    {
        // A plugin holding twenty skills would otherwise produce twenty-one identical findings for one edit.
        var plugin = PublisherChange("company", "someone-else");
        var skill = PublisherChange("company", "someone-else") with
        {
            Name = "deploy-review",
            Kind = AssetKind.Skill,
            After = Asset("deploy-review", UpdateMode.Unknown, "someone-else") with
            {
                DistributedBy = "plugins/deploy",
            },
        };

        var analysis = UpdateAnalyzer.Analyze(
            "/base",
            "/target",
            UpdateMode.Pinned,
            new ProvenanceDiff("/base", "/target", [plugin, skill], [], []),
            NoCapabilityChange);

        analysis.Findings.Should().ContainSingle(finding => finding.Code == "SF5401");
    }

    private static ProvenanceDiff NoDrift => new("/base", "/target", [], [], []);

    private static CapabilitySurfaceDiff NoCapabilityChange => new(
        SurfaceSetDiff.Unchanged,
        SurfaceSetDiff.Unchanged,
        SurfaceSetDiff.Unchanged,
        SurfaceSetDiff.Unchanged,
        SurfaceSetDiff.Unchanged,
        SurfaceSetDiff.Unchanged,
        SurfaceSetDiff.Unchanged);

    private static UpdateAnalysis Analyze(
        UpdateMode mode,
        IReadOnlyList<string>? scripts = null,
        IReadOnlyList<string>? domains = null,
        IReadOnlyList<string>? credentials = null,
        IReadOnlyList<string>? skills = null,
        (string Before, string After)? publisher = null)
    {
        var capabilities = new CapabilitySurfaceDiff(
            new SurfaceSetDiff(skills ?? [], []),
            SurfaceSetDiff.Unchanged,
            SurfaceSetDiff.Unchanged,
            new SurfaceSetDiff(scripts ?? [], []),
            new SurfaceSetDiff(domains ?? [], []),
            new SurfaceSetDiff(credentials ?? [], []),
            SurfaceSetDiff.Unchanged);

        var provenance = publisher is null
            ? NoDrift
            : new ProvenanceDiff(
                "/base",
                "/target",
                [PublisherChange(publisher.Value.Before, publisher.Value.After)],
                [],
                []);

        return UpdateAnalyzer.Analyze("/base", "/target", mode, provenance, capabilities);
    }

    private static AssetProvenanceChange PublisherChange(string before, string after) => new(
        "company-deploy",
        AssetKind.Plugin,
        "plugins/deploy",
        Asset("company-deploy", UpdateMode.Unknown, before),
        Asset("company-deploy", UpdateMode.Unknown, after),
        new SurfaceValueChange(before, after),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private static AssetProvenance Asset(string name, UpdateMode mode, string? publisher = null) => new(
        name,
        AssetKind.Plugin,
        $"plugins/{name}",
        new DistributionSource(null, publisher, null, null, null, "hash", mode),
        ProvenanceStatus.Unknown,
        null,
        0,
        []);
}
