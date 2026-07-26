using SkillForge.Application.Inspection;
using SkillForge.Application.Tests.Validation;
using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Inspection;

namespace SkillForge.Application.Tests.Inspection;

public sealed class SkillInspectorTests
{
    private readonly SkillInspector _inspector = new();

    [Fact]
    public async Task ReportsTheSkillsIdentityAndFiles()
    {
        var skill = new SkillBuilder()
            .WithResources("SKILL.md", "references/notes.md")
            .Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.SkillName.Should().Be("demo-skill");
        inspection.SkillVersion.Should().Be("1.0.0");
        inspection.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task AlwaysReportsFilesystemRead()
    {
        var inspection = await _inspector.InspectAsync(new SkillBuilder().Build(), CancellationToken.None);

        inspection.Capabilities.Should().Equal(SkillCapabilities.FilesystemRead);
    }

    [Fact]
    public async Task AScriptImpliesShellExecutionAndIsNoted()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md", "scripts/analyze.ps1").Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.Capabilities.Should().Contain(SkillCapabilities.ShellExecution);
        inspection.Diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.ContainsScript);
    }

    [Fact]
    public async Task ABinaryFileImpliesBinaryContentAndIsNoted()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md", "assets/logo.png").Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.Capabilities.Should().Contain(SkillCapabilities.BinaryContent);
        inspection.Diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.ContainsBinaryFile);
    }

    [Fact]
    public async Task AnEvalsFolderIsNoted()
    {
        var skill = new SkillBuilder().WithResources("SKILL.md", "evals/cases.json").Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.Diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.ContainsEvals);
    }

    [Fact]
    public async Task FindsExternalUrlsAndImpliesNetworkAccess()
    {
        var skill = new SkillBuilder()
            .WithBody("See [docs](https://learn.microsoft.com/aspnet) and http://example.com/page.")
            .Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.ExternalUrls.Should().Equal(
            "http://example.com/page",
            "https://learn.microsoft.com/aspnet");
        inspection.Capabilities.Should().Contain(SkillCapabilities.NetworkAccess);
        inspection.Diagnostics.Count(d => d.Code == DiagnosticCodes.ContainsExternalUrl).Should().Be(2);
    }

    [Fact]
    public async Task TheSameUrlTwiceIsReportedOnce()
    {
        var skill = new SkillBuilder()
            .WithBody("https://example.com/a and again https://example.com/a")
            .Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.ExternalUrls.Should().ContainSingle();
    }

    [Fact]
    public async Task TrailingPunctuationIsNotPartOfTheUrl()
    {
        var skill = new SkillBuilder().WithBody("See https://example.com/page.").Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.ExternalUrls.Should().Equal("https://example.com/page");
    }

    [Fact]
    public async Task ReportsTheToolsTheFrontmatterDeclares()
    {
        var skill = new SkillBuilder().WithAllowedTools("filesystem.read").Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.DeclaredTools.Should().Equal("filesystem.read");
    }

    [Fact]
    public async Task ObservationsAreInformationalNeverErrors()
    {
        // Inspect describes; it does not judge. Nothing it finds may fail a build.
        var skill = new SkillBuilder()
            .WithResources("SKILL.md", "scripts/run.sh", "assets/logo.png", "evals/cases.json")
            .WithBody("https://example.com")
            .Build();

        var inspection = await _inspector.InspectAsync(skill, CancellationToken.None);

        inspection.Diagnostics.Should().AllSatisfy(diagnostic =>
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Info));
        inspection.Errors.Should().Be(0);
        inspection.Warnings.Should().Be(0);
    }

    [Fact]
    public async Task IsCancellable()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () =>
            await _inspector.InspectAsync(new SkillBuilder().Build(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RejectsAMissingSkill()
    {
        var act = async () => await _inspector.InspectAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
