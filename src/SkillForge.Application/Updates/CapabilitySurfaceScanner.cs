using SkillForge.Application.Abstractions;
using SkillForge.Application.Inspection;
using SkillForge.Application.Migration;
using SkillForge.Application.Skills;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Skills;
using SkillForge.Domain.Updates;

namespace SkillForge.Application.Updates;

/// <summary>
/// Reads everything a tree of assets can reach.
/// </summary>
/// <remarks>
/// It reuses the parts that already answer these questions — skill discovery, the skill inspector, the MCP
/// configuration readers — rather than parsing anything a second time. That is deliberate: two scanners that
/// disagree about what a skill can reach would make <c>update analyze</c> contradict <c>inspect</c>, and the one
/// people would believe is whichever they ran last.
///
/// Environment variable **values are never read**; the readers take the names and drop the values, and only the
/// names that look like a credential are carried here.
/// </remarks>
public sealed class CapabilitySurfaceScanner
{
    /// <summary>File names that hold MCP server declarations.</summary>
    private static readonly string[] McpFileNames = [".mcp.json", "mcp.json"];

    /// <summary>Directories never walked into, matching <c>SkillDiscovery</c>.</summary>
    private static readonly string[] IgnoredDirectoryNames =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts", "dist"];

    /// <summary>
    /// Substrings that make an environment variable name a credential source.
    /// </summary>
    /// <remarks>
    /// A name-shape heuristic, and stated as one wherever it is reported. It is deterministic and it is checkable
    /// by whoever reads the report — which is the most a tool that never reads the value can honestly offer.
    /// </remarks>
    private static readonly string[] CredentialWords =
        ["token", "key", "secret", "password", "passwd", "credential", "auth", "session", "cookie"];

    private readonly IFileSystem _fileSystem;
    private readonly ISkillDiscovery _discovery;
    private readonly ISkillLoader _loader;
    private readonly ISkillInspector _inspector;
    private readonly IReadOnlyList<IMcpConfigurationReader> _mcpReaders;

    /// <summary>Initialises the scanner.</summary>
    /// <param name="fileSystem">Walks the tree.</param>
    /// <param name="discovery">Finds the skills.</param>
    /// <param name="loader">Loads each skill.</param>
    /// <param name="inspector">Says what each skill's contents imply.</param>
    /// <param name="mcpReaders">One reader per MCP configuration format.</param>
    public CapabilitySurfaceScanner(
        IFileSystem fileSystem,
        ISkillDiscovery discovery,
        ISkillLoader loader,
        ISkillInspector inspector,
        IEnumerable<IMcpConfigurationReader> mcpReaders)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(mcpReaders);

        _fileSystem = fileSystem;
        _discovery = discovery;
        _loader = loader;
        _inspector = inspector;
        _mcpReaders = [.. mcpReaders];
    }

    /// <summary>Reads what a tree can reach.</summary>
    /// <param name="path">Directory to scan.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The surface, every list ordered so two scans can be compared.</returns>
    public async Task<CapabilitySurface> ScanAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = _fileSystem.GetFullPath(path);

        if (!_fileSystem.DirectoryExists(root))
        {
            return CapabilitySurface.Empty;
        }

        var skills = new List<string>();
        var capabilities = new List<string>();
        var domains = new List<string>();

        foreach (var directory in _discovery.FindSkillDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var load = await _loader.LoadAsync(directory, cancellationToken).ConfigureAwait(false);
            if (load.Value is not { } skill)
            {
                // A skill that will not load is still installed, so it is named. What it can reach is what
                // 'validate' is for.
                skills.Add(Path.GetFileName(directory.TrimEnd('/', '\\')));
                continue;
            }

            var inspection = await _inspector.InspectAsync(skill, cancellationToken).ConfigureAwait(false);

            skills.Add(skill.Name.Length > 0 ? skill.Name : Path.GetFileName(directory.TrimEnd('/', '\\')));
            capabilities.AddRange(inspection.Capabilities);
            domains.AddRange(inspection.ExternalUrls.Select(HostOf).OfType<string>());
        }

        var servers = await ReadMcpServersAsync(root, cancellationToken).ConfigureAwait(false);

        domains.AddRange(servers
            .Where(server => server.Transport != McpTransport.Stdio)
            .Select(server => HostOf(server.Command))
            .OfType<string>());

        var files = TreeFiles(root).ToArray();

        return new CapabilitySurface(
            Ordered(skills),
            Ordered(servers.Select(server => server.Name)),
            Ordered(files.Where(IsHookFile)),
            Ordered(files.Where(file => SkillResourceClassifier.Classify(file) == SkillResourceKind.Script)),
            Ordered(domains),
            Ordered(servers
                .SelectMany(server => server.EnvironmentVariableNames)
                .Where(IsCredentialShaped)),
            Ordered(capabilities));
    }

    /// <summary>
    /// Reads every MCP configuration in the tree. A file that cannot be read contributes nothing and is not
    /// reported here: <c>mcp inspect</c> and <c>inventory</c> are where an unreadable configuration is a finding,
    /// and this scanner exists to compare two trees rather than to judge either.
    /// </summary>
    private async Task<IReadOnlyList<McpServerDeclaration>> ReadMcpServersAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var servers = new List<McpServerDeclaration>();

        foreach (var file in TreeFilePaths(root).Where(IsMcpFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reader = _mcpReaders.FirstOrDefault(candidate => candidate.CanRead(file));
            if (reader is null)
            {
                continue;
            }

            var result = await reader
                .ReadAsync(file, Mcp.McpFileInspector.FileProviderId, cancellationToken)
                .ConfigureAwait(false);

            servers.AddRange(result.Servers);
        }

        return servers;
    }

    /// <summary>Absolute paths of the files in the tree, minus the directories nothing is read from.</summary>
    private IEnumerable<string> TreeFilePaths(string root) =>
        _fileSystem.EnumerateFiles(root).Where(file => !IsIgnored(root, file));

    /// <summary>The same files, expressed relative to the tree so two trees can be compared.</summary>
    private IEnumerable<string> TreeFiles(string root) =>
        TreeFilePaths(root).Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'));

    private static bool IsMcpFile(string path) =>
        McpFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A hook file is one named <c>hooks.json</c>, or any JSON directly inside a <c>hooks</c> directory. Path-based
    /// on purpose: what a hook does is inside the file, and naming the file is what a diff needs.
    /// </summary>
    private static bool IsHookFile(string relativePath)
    {
        var segments = relativePath.Split('/');

        return string.Equals(segments[^1], "hooks.json", StringComparison.OrdinalIgnoreCase)
            || (segments.Length > 1
                && string.Equals(segments[^2], "hooks", StringComparison.OrdinalIgnoreCase)
                && relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCredentialShaped(string environmentVariableName) =>
        CredentialWords.Any(word =>
            environmentVariableName.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static bool IsIgnored(string root, string file) =>
        Path.GetRelativePath(root, file)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The host part of a URL, or <see langword="null"/> when the value is not one. A command line is not a URL,
    /// and reporting <c>npx</c> as a host would be a scanner inventing network reach.
    /// </summary>
    private static string? HostOf(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host is { Length: > 0 }
            ? uri.Host
            : null;

    private static IReadOnlyList<string> Ordered(IEnumerable<string> values) =>
    [
        .. values
            .Where(value => value is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];
}
