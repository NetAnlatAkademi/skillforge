using SkillForge.Application.Skills;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillResourceClassifierTests
{
    [Theory]
    [InlineData("SKILL.md", SkillResourceKind.SkillDocument)]
    [InlineData("skill.md", SkillResourceKind.SkillDocument)]
    [InlineData("references/notes.md", SkillResourceKind.Markdown)]
    [InlineData("references/notes.markdown", SkillResourceKind.Markdown)]
    [InlineData("scripts/analyze.ps1", SkillResourceKind.Script)]
    [InlineData("scripts/run.sh", SkillResourceKind.Script)]
    [InlineData("scripts/tool.py", SkillResourceKind.Script)]
    [InlineData("skillforge.yaml", SkillResourceKind.Data)]
    [InlineData("evals/cases.json", SkillResourceKind.Data)]
    [InlineData("assets/logo.png", SkillResourceKind.Binary)]
    [InlineData("assets/bundle.zip", SkillResourceKind.Binary)]
    [InlineData("LICENSE", SkillResourceKind.Other)]
    [InlineData("notes.unknownext", SkillResourceKind.Other)]
    public void ClassifiesByExtension(string relativePath, SkillResourceKind expected)
    {
        SkillResourceClassifier.Classify(relativePath).Should().Be(expected);
    }

    [Fact]
    public void ExtensionMatchingIsCaseInsensitive()
    {
        SkillResourceClassifier.Classify("scripts/Analyze.PS1").Should().Be(SkillResourceKind.Script);
    }

    [Fact]
    public void OnlyTheRootSkillFileIsTheSkillDocument()
    {
        // A nested SKILL.md belongs to some other skill; it is just Markdown here.
        SkillResourceClassifier.Classify("nested/SKILL.md").Should().Be(SkillResourceKind.Markdown);
    }

    [Fact]
    public void RejectsBlankPaths()
    {
        var act = () => SkillResourceClassifier.Classify("  ");

        act.Should().Throw<ArgumentException>();
    }
}
