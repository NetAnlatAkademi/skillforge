using SkillForge.Application.Migration;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// <see cref="IMcpConfigurationReader"/> stub keyed by path.
/// </summary>
/// <remarks>
/// The adapter tests are about which paths a provider reads, in what order. What a JSON or TOML file actually says
/// is the readers' own business and is covered by the Infrastructure tests against real files.
/// </remarks>
internal sealed class FakeMcpConfigurationReader : IMcpConfigurationReader
{
    private readonly Dictionary<string, string> _serverNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unreadable = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Paths this reader was asked to read, in order.</summary>
    internal List<string> ReadPaths { get; } = [];

    /// <summary>Declares that <paramref name="path"/> holds one server called <paramref name="serverName"/>.</summary>
    internal FakeMcpConfigurationReader WithServer(string path, string serverName)
    {
        _serverNames[Normalise(path)] = serverName;
        return this;
    }

    /// <summary>Declares that <paramref name="path"/> cannot be parsed.</summary>
    internal FakeMcpConfigurationReader WithUnreadable(string path)
    {
        _unreadable.Add(Normalise(path));
        return this;
    }

    public bool CanRead(string path) => true;

    public Task<McpConfigurationReadResult> ReadAsync(
        string path,
        string providerId,
        CancellationToken cancellationToken = default)
    {
        ReadPaths.Add(Normalise(path));

        if (_unreadable.Contains(Normalise(path)))
        {
            return Task.FromResult(McpConfigurationReadResult.Unreadable(path, "stub failure"));
        }

        return Task.FromResult(_serverNames.TryGetValue(Normalise(path), out var name)
            ? McpConfigurationReadResult.Found(
                [new McpServerDeclaration(name, providerId, McpTransport.Stdio, "node", [], [], path)])
            : McpConfigurationReadResult.None);
    }

    private static string Normalise(string path) => path.Replace('\\', '/');
}
