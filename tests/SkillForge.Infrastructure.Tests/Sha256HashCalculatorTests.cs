using System.Text;
using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure.Tests;

public sealed class Sha256HashCalculatorTests
{
    private readonly Sha256HashCalculator _hasher = new();

    [Fact]
    public void MatchesTheKnownHashOfAnEmptyInput()
    {
        _hasher.ComputeSha256([])
            .Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void MatchesTheKnownHashOfAbc()
    {
        _hasher.ComputeSha256(Encoding.UTF8.GetBytes("abc"))
            .Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void IsLowercaseHexadecimal()
    {
        _hasher.ComputeSha256(Encoding.UTF8.GetBytes("SkillForge"))
            .Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
