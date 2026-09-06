using SkillForge.Application.Provenance;
using SkillForge.Domain.Provenance;

namespace SkillForge.Application.Tests.Provenance;

/// <summary>
/// The mapping from what a marketplace entry says to how the plugin updates.
/// </summary>
/// <remarks>
/// Every case here is driven by something the entry states. The one that matters most is the last: an entry that
/// says nothing is <c>Unknown</c>, not <c>Pinned</c>. Reporting the safest mode for the least evidence is how a
/// supply-chain report ends up reassuring somebody about a plugin nobody has looked at.
/// </remarks>
public sealed class UpdateModeInferenceTests
{
    [Fact]
    public void ACommitShaIsPinned()
    {
        Mode(revision: "43ab2c1f9e0d4b8a7c6e5f4a3b2c1d0e9f8a7b6c").Should().Be(UpdateMode.Pinned);
    }

    [Fact]
    public void AnAbbreviatedShaIsPinned()
    {
        Mode(revision: "43ab2c1").Should().Be(UpdateMode.Pinned);
    }

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("1.2.0")]
    [InlineData("2.0.0-beta.1")]
    public void AVersionTagIsPinned(string revision)
    {
        Mode(revision: revision).Should().Be(UpdateMode.Pinned);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("master")]
    [InlineData("latest")]
    [InlineData("release/2026-08")]
    public void ABranchOrAMovingNameIsFloating(string revision)
    {
        Mode(revision: revision).Should().Be(UpdateMode.Floating);
    }

    [Fact]
    public void ARepositoryWithNoRevisionFloats()
    {
        // Whatever the default branch holds at install time is what arrives, which is the definition of floating.
        Mode(revision: null, repository: "https://github.com/example/plugins")
            .Should().Be(UpdateMode.Floating);
    }

    [Fact]
    public void AnExplicitAutoUpdateFlagWinsOverAPinnedRevision()
    {
        // The SHA is what it updates *from*. The entry is saying it will not stay there.
        UpdateModeInference.From(Entry("43ab2c1", automaticUpdate: true)).Should().Be(UpdateMode.Automatic);
    }

    [Fact]
    public void AnEntryThatSaysNothingIsUnknownRatherThanPinned()
    {
        Mode(revision: null).Should().Be(UpdateMode.Unknown);
    }

    [Fact]
    public void NoEntryAtAllIsUnknown()
    {
        UpdateModeInference.From(null).Should().Be(UpdateMode.Unknown);
    }

    [Fact]
    public void ADeclaredModeIsTakenAtItsWord()
    {
        UpdateModeInference.From(new MarketplaceEntry("demo", null, null, null, null, "floating"))
            .Should().Be(UpdateMode.Floating);
    }

    [Fact]
    public void ADeclaredModeNobodyRecognisesFallsBackToTheEvidence()
    {
        UpdateModeInference.From(new MarketplaceEntry("demo", null, "v1.0.0", null, null, "whenever-we-feel-like-it"))
            .Should().Be(UpdateMode.Pinned);
    }

    private static UpdateMode Mode(string? revision, string? repository = null) =>
        UpdateModeInference.From(Entry(revision, repository: repository));

    private static MarketplaceEntry Entry(
        string? revision,
        string? repository = null,
        bool? automaticUpdate = null) =>
        new("demo", repository, revision, null, automaticUpdate, null);
}
