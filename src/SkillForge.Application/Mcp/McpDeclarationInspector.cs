using SkillForge.Domain.Diagnostics;
using SkillForge.Domain.Migration;

namespace SkillForge.Application.Mcp;

/// <summary>
/// Reports what an MCP server's own declaration says, without connecting to it.
/// </summary>
/// <remarks>
/// Everything here is computable from the configuration file, so it costs nothing and runs always. The questions that
/// need the server itself — which protocol versions it supports, which capabilities it declares — belong to the probe,
/// which is opt-in.
///
/// **Every finding in the SF8xxx band is informational**, and that is a decision rather than an oversight.
/// <c>migrate inspect</c> describes and does not judge (ADR-006), it always exits zero, and the unpinned-command check
/// fires on three of the four MCP servers declared on the machine this was written against. A warning that fires on
/// three quarters of real input is the SF1009 shape — true and nagging. As an observation in an inventory it is neither.
/// </remarks>
public sealed class McpDeclarationInspector
{
    /// <summary>
    /// Package runners that resolve a package at launch, where an unpinned name means "whatever is newest today".
    /// </summary>
    private static readonly string[] ResolvingRunners = ["npx", "npm", "pnpm", "pnpx", "yarn", "bunx", "uvx", "uv", "pipx"];

    /// <summary>
    /// Flags and subcommands that are not the package name, so the check looks past them for the real argument.
    /// </summary>
    private static readonly string[] RunnerNoise = ["-y", "--yes", "-q", "--quiet", "exec", "dlx", "run", "tool", "--from"];

    /// <summary>
    /// Inspects one declaration.
    /// </summary>
    /// <remarks>
    /// An instance method although it reads no state: it is injected, and the checks that will join it — comparing a
    /// declaration against a probed protocol version, or against a provider profile — need dependencies. Making it
    /// static now would mean changing every call site to add the first one.
    /// </remarks>
    /// <param name="server">The declaration, as read from a provider's configuration.</param>
    /// <returns>What the declaration itself reveals, as informational findings.</returns>
#pragma warning disable CA1822 // See the remarks above: injected on purpose, and about to need state.
    public IReadOnlyList<Diagnostic> Inspect(McpServerDeclaration server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var findings = new List<Diagnostic>();

        if (DeclaresSseTransport(server))
        {
            findings.Add(Diagnostic.Info(
                DiagnosticCodes.McpDeprecatedTransport,
                $"'{server.Name}' is declared over the HTTP+SSE transport, deprecated since 2025-03-26.",
                server.SourcePath,
                suggestion: "Streamable HTTP replaces it. The deprecation is recorded at "
                    + "https://modelcontextprotocol.io/specification/2026-07-28/deprecated"));
        }

        if (IsPlaintextRemote(server.Command))
        {
            findings.Add(Diagnostic.Info(
                DiagnosticCodes.McpPlaintextEndpoint,
                $"'{server.Name}' is declared at a plaintext http:// URL on a remote host.",
                server.SourcePath,
                suggestion: "Whatever authorises this connection crosses the network in the clear. A loopback "
                    + "address is not reported, because there is no network to cross."));
        }

        if (UnpinnedPackage(server) is { } package)
        {
            findings.Add(Diagnostic.Info(
                DiagnosticCodes.McpServerCommandNotPinned,
                $"'{server.Name}' launches '{package}' without a pinned version, so two launches may not run the "
                    + "same code.",
                server.SourcePath,
                suggestion: $"Pin it, for example '{package}@1.2.3'. This is SF5001's question asked of an MCP "
                    + "declaration rather than a skill's own files."));
        }

        return findings;
    }
#pragma warning restore CA1822

    /// <summary>
    /// Whether the declaration names the deprecated HTTP+SSE transport — either by type, or by the <c>/sse</c> endpoint
    /// convention every SSE server published.
    /// </summary>
    private static bool DeclaresSseTransport(McpServerDeclaration server) =>
        server.Transport == McpTransport.Http
        && server.Command is { } url
        && (url.EndsWith("/sse", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/sse?", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a URL is plaintext and not loopback. Loopback is excluded on purpose: a local server reached over
    /// <c>http://127.0.0.1</c> exposes nothing to a network, and reporting it would train people to ignore the code.
    /// </summary>
    private static bool IsPlaintextRemote(string? command) =>
        command is not null
        && Uri.TryCreate(command, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && !uri.IsLoopback;

    /// <summary>
    /// The package a resolving runner would fetch, when no version is pinned; <see langword="null"/> when the command
    /// pins one, is not a package runner, or is a path to a local binary.
    /// </summary>
    /// <remarks>
    /// A local executable is never reported. <c>C:\...\node_repl.exe</c> is a file on disk that does not change
    /// underneath you, which is the opposite of the property this check is about.
    /// </remarks>
    private static string? UnpinnedPackage(McpServerDeclaration server)
    {
        if (server.Transport != McpTransport.Stdio || server.Command is null)
        {
            return null;
        }

        var runner = Path.GetFileNameWithoutExtension(server.Command);

        if (!ResolvingRunners.Contains(runner, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var package = server.Arguments.FirstOrDefault(argument =>
            !argument.StartsWith('-')
            && !RunnerNoise.Contains(argument, StringComparer.OrdinalIgnoreCase));

        if (package is null)
        {
            return null;
        }

        // A scoped name starts with '@', so the version separator is a later '@' — and "pinned" to 'latest' or 'next'
        // is not pinned at all, it is the same moving target spelled out.
        var separator = package.LastIndexOf('@');
        var version = separator > 0 ? package[(separator + 1)..] : null;

        return version is null
            || version.Equals("latest", StringComparison.OrdinalIgnoreCase)
            || version.Equals("next", StringComparison.OrdinalIgnoreCase)
                ? package
                : null;
    }
}
