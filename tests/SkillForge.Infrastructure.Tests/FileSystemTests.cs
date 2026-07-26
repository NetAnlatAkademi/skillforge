namespace SkillForge.Infrastructure.Tests;

public sealed class FileSystemTests : IDisposable
{
    private readonly FileSystem _fileSystem = new();
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "skillforge-tests",
        Guid.NewGuid().ToString("n"));

    public FileSystemTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ReportsExistenceOfFilesAndDirectories()
    {
        var file = WriteFile("SKILL.md", "content");

        _fileSystem.FileExists(file).Should().BeTrue();
        _fileSystem.DirectoryExists(_root).Should().BeTrue();
        _fileSystem.FileExists(Path.Combine(_root, "missing.md")).Should().BeFalse();
        _fileSystem.DirectoryExists(Path.Combine(_root, "missing")).Should().BeFalse();
    }

    [Fact]
    public void GetFullPathCollapsesRelativeSegments()
    {
        var messy = Path.Combine(_root, "nested", "..", "SKILL.md");

        _fileSystem.GetFullPath(messy).Should().Be(Path.Combine(_root, "SKILL.md"));
    }

    [Fact]
    public async Task ReadsTextContent()
    {
        var file = WriteFile("SKILL.md", "hello");

        var content = await _fileSystem.ReadAllTextAsync(file, CancellationToken.None);

        content.Should().Be("hello");
    }

    [Fact]
    public void ReportsFileSize()
    {
        var file = WriteFile("SKILL.md", "12345");

        _fileSystem.GetFileSizeInBytes(file).Should().Be(5);
    }

    [Fact]
    public void EnumeratesFilesRecursively()
    {
        WriteFile("SKILL.md", "root");
        WriteFile(Path.Combine("references", "notes.md"), "nested");
        WriteFile(Path.Combine("references", "deep", "more.md"), "deeper");

        var files = _fileSystem.EnumerateFiles(_root).ToArray();

        files.Should().HaveCount(3);
        files.Should().Contain(path => path.EndsWith("more.md", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveLinkTargetReturnsNullForARegularFile()
    {
        var file = WriteFile("SKILL.md", "content");

        _fileSystem.ResolveLinkTarget(file).Should().BeNull();
    }

    [Fact]
    public void ResolveLinkTargetFollowsASymbolicLink()
    {
        var target = WriteFile("target.md", "content");
        var link = Path.Combine(_root, "link.md");

        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (IOException)
        {
            // Windows requires Developer Mode or elevation to create symlinks. The guard's behaviour is
            // covered by unit tests against a fake file system; skip rather than fail on such a machine.
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        _fileSystem.ResolveLinkTarget(link).Should().Be(target);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
