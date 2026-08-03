using SkillForge.Domain.Diffing;
using SkillForge.Domain.Mcp;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Compares two MCP configurations by what they would connect to.
/// </summary>
/// <remarks>
/// Pure, and the same shape as the skill differ: two sets of declarations in, one diff out. Servers are matched by
/// name, because a name is what a configuration keys them by and what an agent refers to them as. A server renamed
/// in place therefore reads as one removed and one added, which is the honest answer — a consumer referring to the
/// old name no longer has it.
/// </remarks>
public static class McpConfigurationDiffer
{
    /// <summary>Compares two configurations.</summary>
    /// <param name="before">The earlier configuration.</param>
    /// <param name="after">The later configuration.</param>
    /// <returns>What changed.</returns>
    public static McpConfigurationDiff Compare(
        McpConfigurationInspection before,
        McpConfigurationInspection after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeByName = ByName(before.Servers);
        var afterByName = ByName(after.Servers);

        var changed = new List<McpServerChange>();
        foreach (var (name, afterServer) in afterByName.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!beforeByName.TryGetValue(name, out var beforeServer))
            {
                continue;
            }

            var change = new McpServerChange(
                afterServer.Name,
                SurfaceValueChange.Between(
                    beforeServer.Transport.ToString(),
                    afterServer.Transport.ToString()),
                SurfaceValueChange.Between(beforeServer.Command, afterServer.Command),
                SurfaceSetDiff.Between(beforeServer.Arguments, afterServer.Arguments),
                SurfaceSetDiff.Between(
                    beforeServer.EnvironmentVariableNames,
                    afterServer.EnvironmentVariableNames));

            if (change.Transport is not null
                || change.Command is not null
                || change.Arguments.HasChanges
                || change.EnvironmentVariableNames.HasChanges)
            {
                changed.Add(change);
            }
        }

        return new McpConfigurationDiff(
            before.Path,
            after.Path,
            Names(afterByName.Keys.Except(beforeByName.Keys, StringComparer.OrdinalIgnoreCase)),
            Names(beforeByName.Keys.Except(afterByName.Keys, StringComparer.OrdinalIgnoreCase)),
            changed);
    }

    /// <summary>
    /// Keys the declarations by name, keeping the first when a configuration declares one name twice. A duplicate is
    /// the configuration's own problem, and picking a side arbitrarily would make the diff depend on read order.
    /// </summary>
    private static Dictionary<string, McpServerDeclaration> ByName(
        IEnumerable<McpServerDeclaration> servers)
    {
        var byName = new Dictionary<string, McpServerDeclaration>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in servers)
        {
            byName.TryAdd(server.Name, server);
        }

        return byName;
    }

    private static string[] Names(IEnumerable<string> names) =>
        [.. names.Order(StringComparer.OrdinalIgnoreCase)];
}
