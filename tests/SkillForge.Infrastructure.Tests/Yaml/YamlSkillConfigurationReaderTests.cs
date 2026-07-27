using SkillForge.Domain.Diagnostics;
using SkillForge.Infrastructure.Yaml;

namespace SkillForge.Infrastructure.Tests.Yaml;

public sealed class YamlSkillConfigurationReaderTests : IDisposable
{
    private readonly YamlSkillConfigurationReader _reader = new(new FileSystem());
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-config-tests",
        Guid.NewGuid().ToString("n"));

    public YamlSkillConfigurationReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task AMissingFileYieldsTheDefaultsAndSaysNothing()
    {
        var result = await _reader.ReadAsync(_root);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Strict.Should().BeFalse();
        result.Value.SuppressedCodes.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty("an optional file that is absent is not a finding");
    }

    [Fact]
    public async Task ReadsStrictAndSuppress()
    {
        Write("""
            schemaVersion: 1

            validation:
              strict: true
              suppress:
                - SF1009
                - SF1010
            """);

        var result = await _reader.ReadAsync(_root);

        result.Value!.Strict.Should().BeTrue();
        result.Value.SuppressedCodes.Should().Equal("SF1009", "SF1010");
    }

    [Fact]
    public async Task AcceptsASingleCodeWhereAListIsExpected()
    {
        Write("validation:\n  suppress: SF1009\n");

        var result = await _reader.ReadAsync(_root);

        result.Value!.SuppressedCodes.Should().Equal("SF1009");
    }

    [Fact]
    public async Task AFileWithNoValidationSectionYieldsTheDefaults()
    {
        Write("schemaVersion: 1\n\npackage:\n  version: 1.0.0\n");

        var result = await _reader.ReadAsync(_root);

        result.Value!.SuppressedCodes.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEmptyFileIsAFileWithNothingToSayNotABrokenOne()
    {
        Write(string.Empty);

        var result = await _reader.ReadAsync(_root);

        result.Diagnostics.Should().BeEmpty();
        result.Value!.Strict.Should().BeFalse();
    }

    [Fact]
    public async Task AMalformedFileIsIgnoredWithSF1012RatherThanFailingTheRun()
    {
        // Silently ignoring it would let a suppression the user wrote quietly not apply; failing the run would
        // punish them for a typo in an optional file.
        Write("validation:\n  suppress:\n    - SF1009\n   - SF1010\n");

        var result = await _reader.ReadAsync(_root);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SuppressedCodes.Should().BeEmpty();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(DiagnosticCodes.ConfigurationNotParsable);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("ignored");
        diagnostic.Suggestion.Should().Contain("not being applied");
    }

    [Fact]
    public async Task StrictIsFalseWhenTheValueIsNotABoolean()
    {
        Write("validation:\n  strict: perhaps\n");

        var result = await _reader.ReadAsync(_root);

        result.Value!.Strict.Should().BeFalse();
    }

    [Fact]
    public async Task RejectsABlankDirectory()
    {
        var act = async () => await _reader.ReadAsync("  ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void RejectsAMissingFileSystem()
    {
        var act = () => new YamlSkillConfigurationReader(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string content) =>
        File.WriteAllText(Path.Combine(_root, "skillforge.yaml"), content);
}
