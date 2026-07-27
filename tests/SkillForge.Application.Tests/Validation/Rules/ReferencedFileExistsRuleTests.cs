using SkillForge.Application.Tests.Validation;
using SkillForge.Application.Validation.Rules;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Application.Tests.Validation.Rules;

public sealed class ReferencedFileExistsRuleTests
{
    private readonly ReferencedFileExistsRule _rule = new();

    [Fact]
    public void ReportsTheCodeItOwns() => _rule.Code.Should().Be(DiagnosticCodes.ReferencedFileNotFound);

    [Fact]
    public async Task SaysNothingWhenEveryReferenceResolves()
    {
        var skill = new SkillBuilder()
            .WithBody("See [notes](references/notes.md).")
            .WithResources("SKILL.md", "references/notes.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsAReferenceThatDoesNotResolve()
    {
        var skill = new SkillBuilder()
            .WithBody("See [checklist](references/checklist.md).", bodyStartLine: 10)
            .WithResources("SKILL.md")
            .Build();

        var diagnostic = (await _rule.Run(skill)).Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ReferencedFileNotFound);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Line.Should().Be(10);
        diagnostic.Message.Should().Contain("references/checklist.md");
    }

    [Fact]
    public async Task ReportsEveryBrokenReferenceOnce()
    {
        var skill = new SkillBuilder()
            .WithBody("[a](references/a.md)\n[b](scripts/b.ps1)\n[a again](references/a.md)")
            .WithResources("SKILL.md")
            .Build();

        var diagnostics = await _rule.Run(skill);

        // The same missing file mentioned twice is one problem, reported once, at its first mention.
        diagnostics.Should().HaveCount(2);
        diagnostics.Select(diagnostic => diagnostic.Line).Should().Equal(6, 7);
    }

    [Fact]
    public async Task IgnoresExternalAndAnchorLinks()
    {
        var skill = new SkillBuilder()
            .WithBody("""
                [docs](https://learn.microsoft.com/)
                [insecure](http://example.com/page)
                [mail](mailto:someone@example.com)
                [section](#workflow)
                """)
            .WithResources("SKILL.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task IgnoresLinksInsideFencedCodeBlocks()
    {
        var skill = new SkillBuilder()
            .WithBody("""
                Example of what a reference looks like:

                ```markdown
                [notes](references/does-not-exist.md)
                ```
                """)
            .WithResources("SKILL.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }

    [Fact]
    public async Task MatchesReferencesWrittenWithALeadingDotSlash()
    {
        var skill = new SkillBuilder()
            .WithBody("See [notes](./references/notes.md).")
            .WithResources("SKILL.md", "references/notes.md")
            .Build();

        (await _rule.Run(skill)).Should().BeEmpty();
    }
}
