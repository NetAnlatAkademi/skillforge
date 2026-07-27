using SkillForge.Application.Skills;

namespace SkillForge.Application.Tests.Skills;

public sealed class SkillNameTests
{
    [Theory]
    [InlineData("demo")]
    [InlineData("demo-skill")]
    [InlineData("dotnet-api-review")]
    [InlineData("a-b-c-d")]
    [InlineData("skill2")]
    public void AcceptsUsableNames(string name)
    {
        SkillName.DescribeProblem(name).Should().BeNull();
        SkillName.IsValid(name).Should().BeTrue();
    }

    [Fact]
    public void RejectsANameShorterThanTheMinimum()
    {
        SkillName.DescribeProblem("d").Should().NotBeNull();
        SkillName.IsValid("d").Should().BeFalse();
    }

    [Fact]
    public void RejectsANameLongerThanTheMaximum()
    {
        var name = new string('a', SkillName.MaximumLength + 1);

        SkillName.DescribeProblem(name).Should().NotBeNull();
        SkillName.IsValid(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("Demo")]
    [InlineData("demo_skill")]
    [InlineData("demo skill")]
    [InlineData("2demo")]
    [InlineData("demo--skill")]
    public void RejectsNamesThatAreNotUsableIdentifiers(string name)
    {
        SkillName.DescribeProblem(name).Should().NotBeNull();
        SkillName.IsValid(name).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANullOrBlankNameHasNoDescribedProblem(string? name)
    {
        // Whether a name is required at all is SF0004's job, not this type's. DescribeProblem only
        // judges the shape of a name that is actually present.
        SkillName.DescribeProblem(name).Should().BeNull();

        // IsValid still reports false: a blank name is not usable, even though DescribeProblem stays silent.
        SkillName.IsValid(name).Should().BeFalse();
    }
}
