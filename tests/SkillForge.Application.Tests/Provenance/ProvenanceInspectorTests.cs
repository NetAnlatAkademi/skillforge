using SkillForge.Application.Provenance;
using SkillForge.Application.Skills;
using SkillForge.Application.Tests.Fakes;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Tests.Provenance;

/// <summary>
/// What the inspector may say, and — mostly — what it may not.
/// </summary>
/// <remarks>
/// The tests that matter here are the negative ones. A provenance report is believed, so every field it fills in
/// has to come from a file somebody wrote: an asset outside a repository has no origin, a plugin nobody lists has
/// no marketplace, and neither gets one invented for it.
/// </remarks>
public sealed class ProvenanceInspectorTests
{
    private const string Root = "/repo";
    private const string Remote = "https://github.com/example/skills.git";
    private const string Commit = "43ab2c1f9e0d4b8a7c6e5f4a3b2c1d0e9f8a7b6c";

    [Fact]
    public async Task ASkillInACleanCheckoutIsTracedToItsRepository()
    {
        var report = await Inspect(SkillLayout(), Repository());

        var skill = report.Assets.Should().ContainSingle().Subject;
        skill.Name.Should().Be("dotnet-api-review");
        skill.Kind.Should().Be(AssetKind.Skill);
        skill.Path.Should().Be("skills/dotnet-api-review");
        skill.Source.RepositoryUrl.Should().Be(Remote);
        skill.Source.CommitSha.Should().Be(Commit);
        skill.Source.Sha256.Should().NotBeNullOrEmpty();
        skill.Status.Should().Be(ProvenanceStatus.VerifiedSource);
    }

    [Fact]
    public async Task OutsideARepositoryNothingIsInvented()
    {
        var report = await Inspect(SkillLayout(), new FakeRepositoryFactsReader());

        var skill = report.Assets.Should().ContainSingle().Subject;
        skill.Source.RepositoryUrl.Should().BeNull();
        skill.Source.CommitSha.Should().BeNull();
        skill.Source.Publisher.Should().BeNull();
        skill.Source.Marketplace.Should().BeNull();
        skill.Source.UpdateMode.Should().Be(UpdateMode.Unknown);
        skill.Status.Should().Be(ProvenanceStatus.Unknown);
        skill.DeclaredUpstream.Should().BeNull();
    }

    [Fact]
    public async Task ASkillIsStillFingerprintedWhenNothingElseCanBeSaidAboutIt()
    {
        // The one field that is always computable, and the reason a report of unknowns is still worth having.
        var report = await Inspect(SkillLayout(), new FakeRepositoryFactsReader());

        report.Assets[0].Source.Sha256.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UncommittedChangesAreCountedAndSaidSo()
    {
        var facts = Repository()
            .WithModified("/repo/skills/dotnet-api-review", " M SKILL.md", " M references/checklist.md");

        var report = await Inspect(SkillLayout(), facts);

        report.Assets[0].LocallyModifiedFiles.Should().Be(2);
        report.Assets[0].Status.Should().Be(ProvenanceStatus.Modified);
    }

    [Fact]
    public async Task APluginTakesItsPublisherAndMarketplaceFromWhatWasRead()
    {
        var report = await Inspect(PluginLayout(), Repository(), Manifests());

        var plugin = report.Assets.Single(asset => asset.Kind == AssetKind.Plugin);
        plugin.Name.Should().Be("company-deploy");
        plugin.Source.Publisher.Should().Be("company");
        plugin.Source.Marketplace.Should().Be("company-internal");
        plugin.Source.Version.Should().Be("1.4.2");
        plugin.Evidence.Should().Contain("plugins/deploy/.claude-plugin/plugin.json");
    }

    [Fact]
    public async Task AFloatingPluginIsUnpinnedRatherThanVerified()
    {
        var manifests = Manifests(revision: "main");

        var report = await Inspect(PluginLayout(), Repository(), manifests);

        var plugin = report.Assets.Single(asset => asset.Kind == AssetKind.Plugin);
        plugin.Source.UpdateMode.Should().Be(UpdateMode.Floating);
        plugin.Status.Should().Be(ProvenanceStatus.Unpinned);
    }

    [Fact]
    public async Task AManifestThatCouldNotBeReadIsReportedRatherThanSkipped()
    {
        // A plugin that vanishes from the report because its manifest failed to parse is a silent gap, and a
        // silent gap in a provenance report reads as "there is nothing here".
        var report = await Inspect(PluginLayout(), Repository(), new FakeDistributionManifestReader());

        report.Assets.Should().NotContain(asset => asset.Kind == AssetKind.Plugin);
        report.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Code == "SF1015");
        report.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.FilePath == "/repo/plugins/deploy/.claude-plugin/plugin.json");
    }

    [Fact]
    public async Task ASkillInsideAPluginIsListedInItsOwnRight()
    {
        // A plugin's version can stay put while a skill inside it is edited, which is exactly the case a
        // per-asset fingerprint exists to catch.
        var report = await Inspect(PluginLayout(), Repository(), Manifests());

        report.Assets.Should().HaveCount(2);
        report.Assets.Should().Contain(asset => asset.Kind == AssetKind.Skill && asset.Name == "deploy-review");
    }

    [Fact]
    public async Task ASkillInsideAPluginInheritsHowThePluginIsDistributed()
    {
        // What arrives in the plugin's next version arrives in the skill too, so reporting the skill as having no
        // marketplace and no update mode would hide the thing that actually decides what runs.
        var report = await Inspect(PluginLayout(), Repository(), Manifests(revision: "main"));

        var skill = report.Assets.Single(asset => asset.Kind == AssetKind.Skill);
        skill.Source.Marketplace.Should().Be("company-internal");
        skill.Source.Publisher.Should().Be("company");
        skill.Source.UpdateMode.Should().Be(UpdateMode.Floating);
        skill.Status.Should().Be(ProvenanceStatus.Unpinned);
    }

    [Fact]
    public async Task ASkillOutsideAnyPluginInheritsNothing()
    {
        var report = await Inspect(SkillLayout(), Repository(), Manifests());

        var skill = report.Assets.Single(asset => asset.Kind == AssetKind.Skill);
        skill.Source.Marketplace.Should().BeNull();
        skill.Source.Publisher.Should().BeNull();
        skill.Source.UpdateMode.Should().Be(UpdateMode.Unknown);
    }

    [Fact]
    public async Task ASkillKeepsItsOwnFingerprintWhenItInherits()
    {
        var report = await Inspect(PluginLayout(), Repository(), Manifests());

        var plugin = report.Assets.Single(asset => asset.Kind == AssetKind.Plugin);
        var skill = report.Assets.Single(asset => asset.Kind == AssetKind.Skill);

        skill.Source.Sha256.Should().NotBe(plugin.Source.Sha256);
    }

    [Fact]
    public async Task TheDistributionSummaryCountsWhatWasObserved()
    {
        var report = await Inspect(PluginLayout(), Repository(), Manifests(revision: "main"));

        report.Distribution.Marketplaces.Should().Be(1);

        // Two: the plugin, and the skill inside it that arrives with every one of its updates.
        report.Distribution.FloatingAssets.Should().Be(2);
        report.Distribution.PinnedAssets.Should().Be(0);
        report.Distribution.AutomaticUpdates.Should().Be(0);
        report.Distribution.UnknownProvenance.Should().Be(0);
    }

    [Fact]
    public async Task AssetsComeBackOrderedByPath()
    {
        var layout = SkillLayout()
            .AddFile("/repo/skills/abc/SKILL.md", "# abc")
            .AddFile("/repo/skills/xyz/SKILL.md", "# xyz");

        var report = await Inspect(layout, Repository());

        report.Assets.Select(asset => asset.Path).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task AChangedMarketplaceIsReportedAsSF5201()
    {
        var findings = Drift(
            await Inspect(PluginLayout(), Repository(), Manifests()),
            await Inspect(PluginLayout(), Repository(), Manifests(marketplace: "community")));

        findings.Should().Contain(finding => finding.Code == "SF5201");
    }

    [Fact]
    public async Task AnUpstreamThatAppearsIsReportedAsSF5102()
    {
        // The manifest now names a repository the marketplace does not distribute from — a fork, in the asset's
        // own words. Information, not a warning: forking is ordinary and only the reviewer knows if it was meant.
        var findings = Drift(
            await Inspect(PluginLayout(), Repository(), Manifests()),
            await Inspect(PluginLayout(), Repository(), Manifests(declaredRepository: "https://github.com/me/fork")));

        var finding = findings.Single(candidate => candidate.Code == "SF5102");
        finding.Severity.Should().Be(Domain.Diagnostics.DiagnosticSeverity.Info);
        finding.Message.Should().Contain("https://github.com/me/fork");
    }

    [Fact]
    public async Task UncommittedChangesThatAppearAreReportedAsSF5103()
    {
        var before = await Inspect(SkillLayout(), Repository());
        var after = await Inspect(
            SkillLayout(),
            Repository().WithModified("/repo/skills/dotnet-api-review", " M SKILL.md"));

        Drift(before, after).Should().Contain(finding => finding.Code == "SF5103");
    }

    [Fact]
    public async Task UncommittedChangesThatWereAlreadyThereAreNotReportedAgain()
    {
        // Otherwise a working copy somebody is mid-edit in produces the same finding on every run, which is how a
        // finding becomes something people filter out.
        var facts = Repository().WithModified("/repo/skills/dotnet-api-review", " M SKILL.md");

        var findings = Drift(
            await Inspect(SkillLayout(), facts),
            await Inspect(SkillLayout(), facts));

        findings.Should().BeEmpty();
    }

    private static IReadOnlyList<Domain.Diagnostics.Diagnostic> Drift(
        ProvenanceReport before,
        ProvenanceReport after) =>
        ProvenanceChangeDiagnostics.From(ProvenanceDiffer.Compare(before, after));

    private static FakeFileSystem SkillLayout() =>
        new FakeFileSystem()
            .AddFile("/repo/skills/dotnet-api-review/SKILL.md", "# dotnet-api-review")
            .AddFile("/repo/skills/dotnet-api-review/references/checklist.md", "one");

    private static FakeFileSystem PluginLayout() =>
        new FakeFileSystem()
            .AddFile("/repo/plugins/deploy/.claude-plugin/plugin.json", "{}")
            .AddFile("/repo/.claude-plugin/marketplace.json", "{}")
            .AddFile("/repo/plugins/deploy/skills/deploy-review/SKILL.md", "# deploy-review");

    private static FakeRepositoryFactsReader Repository() =>
        new FakeRepositoryFactsReader().InRepository(Root, Remote, Commit);

    private static FakeDistributionManifestReader Manifests(
        string? revision = "1.4.2",
        string marketplace = "company-internal",
        string? declaredRepository = null) =>
        new FakeDistributionManifestReader()
            .WithPlugin(
                "/repo/plugins/deploy/.claude-plugin/plugin.json",
                new PluginManifest(
                    "company-deploy",
                    "1.4.2",
                    "company",
                    declaredRepository,
                    "/repo/plugins/deploy/.claude-plugin/plugin.json"))
            .WithMarketplace(
                "/repo/.claude-plugin/marketplace.json",
                new MarketplaceListing(
                    marketplace,
                    "company",
                    "/repo/.claude-plugin/marketplace.json",
                    [new MarketplaceEntry("company-deploy", Remote, revision, "1.4.2", null, null)]));

    private static async Task<ProvenanceReport> Inspect(
        FakeFileSystem fileSystem,
        FakeRepositoryFactsReader facts,
        FakeDistributionManifestReader? manifests = null)
    {
        var loader = new FakeSkillLoader()
            .WithSkill("/repo/skills/dotnet-api-review", "dotnet-api-review")
            .WithSkill("/repo/plugins/deploy/skills/deploy-review", "deploy-review");

        var inspector = new ProvenanceInspector(
            fileSystem,
            new SkillDiscovery(fileSystem),
            loader,
            facts,
            manifests ?? new FakeDistributionManifestReader(),
            new AssetFingerprinter(fileSystem, new FakeHashCalculator()));

        return await inspector.InspectAsync(Root, includeLocalModifications: true, CancellationToken.None);
    }
}
