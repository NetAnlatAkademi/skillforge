using SkillForge.Application.Skills;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillTemplateTests
{
    [Fact]
    public void TheGeneratedSkillFileHasFrontmatterAndABody()
    {
        var content = SkillTemplate.CreateSkillFile(new SkillTemplateOptions("dotnet-api-review"));

        content.Should().StartWith("---");
        content.Should().Contain("name: dotnet-api-review");
        content.Should().Contain("# Dotnet Api Review");
        content.Should().Contain("## When to use this");
    }

    [Fact]
    public void ThePlaceholderDescriptionStatesAnActivationContext()
    {
        // Otherwise init would generate a skill that immediately warns about SF1002.
        var content = SkillTemplate.CreateSkillFile(new SkillTemplateOptions("demo-skill"));

        content.Should().Contain("Use this skill when");
    }

    [Fact]
    public void TheConfigurationFileDeclaresNoPermissionsByDefault()
    {
        var content = SkillTemplate.CreateConfigurationFile(new SkillTemplateOptions("demo-skill"));

        content.Should().Contain("schemaVersion: 1");
        content.Should().Contain("allowed: false");
        content.Should().Contain("secrets: []");
    }

    [Fact]
    public void RejectsMissingOptions()
    {
        var skillFile = () => SkillTemplate.CreateSkillFile(null!);
        var configuration = () => SkillTemplate.CreateConfigurationFile(null!);

        skillFile.Should().Throw<ArgumentNullException>();
        configuration.Should().Throw<ArgumentNullException>();
    }
}
