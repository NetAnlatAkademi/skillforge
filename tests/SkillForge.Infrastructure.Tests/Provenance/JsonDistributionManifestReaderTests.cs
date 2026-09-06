using SkillForge.Infrastructure.Provenance;

namespace SkillForge.Infrastructure.Tests.Provenance;

/// <summary>
/// The reader takes what a manifest says and nothing else.
/// </summary>
/// <remarks>
/// The shapes tested here are the ones real files use: an author as a string and as an object, a source nested
/// under <c>source</c> and flattened onto the entry, and a file with comments in it. A parser that only accepts
/// the tidiest of them reports working configurations as corrupt.
/// </remarks>
public sealed class JsonDistributionManifestReaderTests : IDisposable
{
    private readonly JsonDistributionManifestReader _reader = new(new FileSystem());
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-manifest-tests",
        Guid.NewGuid().ToString("n"));

    public JsonDistributionManifestReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadsAPluginManifest()
    {
        var path = Write("plugin.json", """
            {
              "name": "company-deploy",
              "version": "1.4.2",
              "author": { "name": "company" },
              "repository": "https://github.com/company/plugins"
            }
            """);

        var result = await _reader.ReadPluginAsync(path, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("company-deploy");
        result.Value.Version.Should().Be("1.4.2");
        result.Value.Publisher.Should().Be("company");
        result.Value.RepositoryUrl.Should().Be("https://github.com/company/plugins");
    }

    [Fact]
    public async Task AnAuthorWrittenAsAStringMeansTheSameThing()
    {
        var path = Write("plugin.json", """{ "name": "demo", "author": "kim" }""");

        var result = await _reader.ReadPluginAsync(path, CancellationToken.None);

        result.Value!.Publisher.Should().Be("kim");
    }

    [Fact]
    public async Task AManifestThatNamesNothingIsStillAManifest()
    {
        // Named after its directory, and every other field left unset. Refusing it would be a parser being right
        // at the expense of a file the tool that owns it accepts.
        var path = Write("plugin.json", "{}");

        var result = await _reader.ReadPluginAsync(path, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().BeNull();
        result.Value.Publisher.Should().BeNull();
        result.Value.RepositoryUrl.Should().BeNull();
    }

    [Fact]
    public async Task ReadsAMarketplaceWithItsEntries()
    {
        var path = Write("marketplace.json", """
            {
              // A comment, which real files have.
              "name": "company-internal",
              "owner": "company",
              "plugins": [
                {
                  "name": "company-deploy",
                  "source": { "source": "github", "repo": "company/deploy", "ref": "v1.4.2" }
                },
                {
                  "name": "company-review",
                  "repository": "https://github.com/company/review",
                  "branch": "main",
                  "autoUpdate": true
                },
              ]
            }
            """);

        var result = await _reader.ReadMarketplaceAsync(path, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("company-internal");
        result.Value.Publisher.Should().Be("company");
        result.Value.Entries.Should().HaveCount(2);

        var deploy = result.Value.Entries[0];
        deploy.RepositoryUrl.Should().Be("company/deploy");
        deploy.Revision.Should().Be("v1.4.2");
        deploy.AutomaticUpdate.Should().BeNull();

        var review = result.Value.Entries[1];
        review.Revision.Should().Be("main");
        review.AutomaticUpdate.Should().BeTrue();
    }

    [Fact]
    public async Task AMarketplaceThatListsBareNamesIsRead()
    {
        var path = Write("marketplace.json", """{ "name": "local", "plugins": ["one", "two"] }""");

        var result = await _reader.ReadMarketplaceAsync(path, CancellationToken.None);

        result.Value!.Entries.Select(entry => entry.PluginName).Should().Equal("one", "two");
        result.Value.Entries.Should().OnlyContain(entry => entry.Revision == null);
    }

    [Fact]
    public async Task AFileThatIsNotThereIsReportedRatherThanThrown()
    {
        var result = await _reader.ReadPluginAsync(
            Path.Combine(_root, "missing.json"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Code.Should().Be("SF1015");
    }

    [Fact]
    public async Task UnparsableJsonIsReportedRatherThanThrown()
    {
        var path = Write("plugin.json", "{ this is not json");

        var result = await _reader.ReadPluginAsync(path, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Code.Should().Be("SF1015");
    }

    [Fact]
    public async Task AJsonArrayAtTheRootIsNotAManifest()
    {
        var path = Write("plugin.json", "[1, 2, 3]");

        var result = await _reader.ReadPluginAsync(path, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Write(string name, string content)
    {
        var directory = Path.Combine(_root, ".claude-plugin");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);

        return path;
    }
}
