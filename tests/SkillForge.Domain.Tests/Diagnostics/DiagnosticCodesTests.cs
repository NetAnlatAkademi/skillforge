using System.Globalization;
using System.Reflection;
using SkillForge.Domain.Diagnostics;

namespace SkillForge.Domain.Tests.Diagnostics;

/// <summary>
/// What this pins is the code set's <em>shape</em>, not its size.
/// </summary>
/// <remarks>
/// An earlier version asserted "exactly 24 codes", which encoded a stance the project later reversed on purpose:
/// the set is open, and new bands are planned. Counting codes made the test fail every time one was added, which
/// teaches whoever adds one to edit the number and move on. These assertions instead catch the mistakes that
/// actually matter — a duplicate, a renumbering, a gap left by a deleted code — because a released code's
/// identity is what every CI suppression depends on.
/// </remarks>
public sealed class DiagnosticCodesTests
{
    private static readonly IReadOnlyList<string> AllCodes = typeof(DiagnosticCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(field => field is { IsLiteral: true, IsInitOnly: false })
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void EveryCodeIsUnique()
    {
        // A reused code would silently change the meaning of a rule for anyone suppressing it.
        AllCodes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryCodeFollowsTheNamingScheme()
    {
        AllCodes.Should().AllSatisfy(code =>
        {
            code.Should().MatchRegex("^SF[0-9][0-9]{3}$");
        });
    }

    [Fact]
    public void NoCodeIsEverDeclaredAsZero()
    {
        // Numbering starts at 0001 in every band, so a 0000 means somebody miscounted rather than reserved.
        AllCodes.Should().NotContain(code => code.EndsWith("000", StringComparison.Ordinal));
    }

    [Fact]
    public void EachBlockIsNumberedContiguouslyFromOne()
    {
        // A gap means a code was deleted or renumbered — the one thing a published code must never do. Adding
        // the next number in a block is always fine, which is why this does not count anything.
        //
        // Blocks of a hundred rather than whole bands, because a band may hold more than one: SF8001-SF8009 are
        // what an MCP server's declaration says, SF8101 onwards are an organisation's decisions about it. Both are
        // MCP, so both are SF8xxx; the gap between them is the deliberate part, and grouping by band would read it
        // as the accident this test exists to catch.
        foreach (var block in AllCodes.GroupBy(code => code[..4]))
        {
            var numbers = block
                .Select(code => int.Parse(code[2..], CultureInfo.InvariantCulture) % 100)
                .Order()
                .ToArray();

            numbers.Should().Equal(
                Enumerable.Range(1, numbers.Length),
                $"block {block.Key}xx must run from 1 with no gaps, but is {string.Join(", ", numbers)}");
        }
    }

    [Fact]
    public void TheThreeShippedBandsAreAllPresent()
    {
        AllCodes.Select(code => code[..3]).Distinct().Should().Contain(["SF0", "SF1", "SF2"]);
    }
}
