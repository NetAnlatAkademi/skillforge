using SkillForge.Application.Abstractions;
using SkillForge.Application.Identity;
using SkillForge.Application.Inspection;
using SkillForge.Application.Migration;
using SkillForge.Application.Provenance;
using SkillForge.Domain.Graph;
using SkillForge.Domain.Migration;
using SkillForge.Domain.Skills;

namespace SkillForge.Application.Graph;

/// <summary>
/// Reads a directory into a graph of what is wired to what.
/// </summary>
/// <remarks>
/// **Nothing here parses anything a second time.** Skills come from the same discovery and loader
/// <c>validate</c> uses, their contents from the same inspector <c>inspect</c> uses, MCP servers from the same
/// format readers <c>mcp inspect</c> uses, plugins from the same manifest reader <c>provenance</c> uses, and
/// identities from the same inference <c>identity inspect</c> uses. A second scanner would let the graph
/// contradict the commands, and the one people would believe is whichever they ran last.
///
/// What it adds is the edges — and it adds only edges it can cite. Every one carries a file and, when the claim was
/// read out of the file's text rather than out of its existence, a line. There is no transitive closure and no
/// "probably": an edge SkillForge invented would look, on the diagram, exactly like one it read.
///
/// **No value that could be a credential is read.** Credential nodes are environment variable and header *names*,
/// which is all the MCP readers ever put in the model in the first place.
/// </remarks>
public sealed class GraphBuilder
{
    /// <summary>File names that hold MCP server declarations, matching <c>CapabilitySurfaceScanner</c>.</summary>
    private static readonly string[] McpFileNames = [".mcp.json", "mcp.json"];

    /// <summary>
    /// Instruction files an agent reads. The names come from the provider adapters — Claude Code's
    /// <c>CLAUDE.md</c> and <c>AGENTS.md</c>, Codex's <c>AGENTS.md</c>, Copilot's
    /// <c>copilot-instructions.md</c> — rather than from a guess about what people call such a file.
    /// </summary>
    private static readonly string[] InstructionFileNames =
        ["CLAUDE.md", "AGENTS.md", "copilot-instructions.md"];

    /// <summary>Directories never walked into, matching <c>SkillDiscovery</c>.</summary>
    private static readonly string[] IgnoredDirectoryNames =
        [".git", ".github", ".vs", ".idea", "bin", "obj", "node_modules", "artifacts", "dist"];

    /// <summary>
    /// Substrings that make an environment variable or header name a credential source. The same list
    /// <c>CapabilitySurfaceScanner</c> uses, because two answers to "is this a credential" would be one answer too
    /// many.
    /// </summary>
    private static readonly string[] CredentialWords =
        ["token", "key", "secret", "password", "passwd", "credential", "auth", "session", "cookie"];

    private readonly IFileSystem _fileSystem;
    private readonly ISkillDiscovery _discovery;
    private readonly ISkillLoader _loader;
    private readonly ISkillInspector _inspector;
    private readonly IReadOnlyList<IMcpConfigurationReader> _mcpReaders;
    private readonly IDistributionManifestReader _manifests;

    /// <summary>Initialises the builder.</summary>
    /// <param name="fileSystem">Walks the tree and reads the files whose text is evidence.</param>
    /// <param name="discovery">Finds the skills.</param>
    /// <param name="loader">Loads each skill.</param>
    /// <param name="inspector">Says what each skill contains and points at.</param>
    /// <param name="mcpReaders">One reader per MCP configuration format.</param>
    /// <param name="manifests">Reads plugin and marketplace manifests.</param>
    public GraphBuilder(
        IFileSystem fileSystem,
        ISkillDiscovery discovery,
        ISkillLoader loader,
        ISkillInspector inspector,
        IEnumerable<IMcpConfigurationReader> mcpReaders,
        IDistributionManifestReader manifests)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(mcpReaders);
        ArgumentNullException.ThrowIfNull(manifests);

        _fileSystem = fileSystem;
        _discovery = discovery;
        _loader = loader;
        _inspector = inspector;
        _mcpReaders = [.. mcpReaders];
        _manifests = manifests;
    }

    /// <summary>Builds the graph for a directory.</summary>
    /// <param name="path">Directory to read.</param>
    /// <param name="cancellationToken">Token used to cancel the work.</param>
    /// <returns>The graph. Empty when the directory does not exist — an absence, not a failure.</returns>
    public async Task<AssetGraph> BuildAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = _fileSystem.GetFullPath(path);

        if (!_fileSystem.DirectoryExists(root))
        {
            return AssetGraph.Empty(root);
        }

        var collector = new GraphCollector();
        var files = TreeFilePaths(root).ToArray();

        var servers = await AddMcpServersAsync(root, files, collector, cancellationToken).ConfigureAwait(false);
        await AddPluginsAsync(root, files, collector, cancellationToken).ConfigureAwait(false);
        await AddSkillsAsync(root, servers, collector, cancellationToken).ConfigureAwait(false);
        await AddInstructionFilesAsync(root, files, servers, collector, cancellationToken).ConfigureAwait(false);
        await AddHooksAsync(root, files, collector, cancellationToken).ConfigureAwait(false);

        return collector.Build(root);
    }

    /// <summary>
    /// Reads every MCP configuration in the tree, and everything each declared server reaches.
    /// </summary>
    /// <returns>The declarations, so the skill and instruction passes can look for their names.</returns>
    private async Task<IReadOnlyList<McpServerDeclaration>> AddMcpServersAsync(
        string root,
        IReadOnlyList<string> files,
        GraphCollector collector,
        CancellationToken cancellationToken)
    {
        var servers = new List<McpServerDeclaration>();

        foreach (var file in files.Where(IsMcpFile))
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

            // A configuration that would not parse is reported: a missing node in a graph is worse than a stated gap.
            collector.Report(result.Diagnostics);
            servers.AddRange(result.Servers);
        }

        foreach (var server in servers)
        {
            AddServer(root, server, collector);
        }

        return servers;
    }

    private static void AddServer(string root, McpServerDeclaration server, GraphCollector collector)
    {
        var source = GraphIds.Relative(root, server.SourcePath);
        var serverId = collector.Node(GraphNodeType.McpServer, server.Name, source);

        if (HostOf(server.Command) is { } host)
        {
            collector.Edge(
                serverId,
                collector.Node(GraphNodeType.ExternalHost, host),
                GraphRelation.ConnectsTo,
                GraphConfidence.Declared,
                new GraphEvidence(source, null, "mcp server url"));
        }

        AddCredentialSources(source, serverId, server, collector);
        AddIdentity(source, serverId, server, collector);
    }

    /// <summary>
    /// Names the declaration reads a credential from — never a value.
    /// </summary>
    /// <remarks>
    /// The **name** is declared: the configuration file says this server is given this variable. Whether the
    /// variable holds a credential is a guess from its spelling, which is why the edge is
    /// <see cref="GraphConfidence.Inferred"/> and why the reports say the categories come from names.
    /// </remarks>
    private static void AddCredentialSources(
        string source,
        string serverId,
        McpServerDeclaration server,
        GraphCollector collector)
    {
        foreach (var name in server.EnvironmentVariableNames.Where(IsCredentialShaped))
        {
            collector.Edge(
                serverId,
                collector.Node(GraphNodeType.CredentialSource, name),
                GraphRelation.ReadsCredential,
                GraphConfidence.Inferred,
                new GraphEvidence(source, null, "declared environment variable name"));
        }

        foreach (var name in server.HeaderNamesOrEmpty.Where(IsCredentialShaped))
        {
            collector.Edge(
                serverId,
                collector.Node(GraphNodeType.CredentialSource, name),
                GraphRelation.ReadsCredential,
                GraphConfidence.Inferred,
                new GraphEvidence(source, null, "declared header name"));
        }
    }

    /// <summary>
    /// The identity a server is reached with, from the same inference <c>identity inspect</c> publishes.
    /// </summary>
    /// <remarks>
    /// No probe. <c>graph</c> talks to nothing, so the identity here is what the declaration's names imply and
    /// never what a server's <c>401</c> said. An <see cref="Domain.Identity.AgentIdentityType.Unknown"/> identity
    /// produces no node: a graph full of "Unknown" boxes is a graph nobody reads.
    /// </remarks>
    private static void AddIdentity(
        string source,
        string serverId,
        McpServerDeclaration server,
        GraphCollector collector)
    {
        var identity = IdentityInference.From(server);

        if (identity.Identity.Type == Domain.Identity.AgentIdentityType.Unknown)
        {
            return;
        }

        collector.Edge(
            serverId,
            collector.Node(GraphNodeType.Identity, $"{server.Name}:{identity.Identity.Type}"),
            GraphRelation.UsesIdentity,
            GraphConfidence.Inferred,
            new GraphEvidence(source, null, "identity inferred from declared names"));
    }

    /// <summary>Reads the plugin manifests, and where each plugin says it comes from.</summary>
    private async Task AddPluginsAsync(
        string root,
        IReadOnlyList<string> files,
        GraphCollector collector,
        CancellationToken cancellationToken)
    {
        foreach (var file in files.Where(IsPluginManifest))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _manifests.ReadPluginAsync(file, cancellationToken).ConfigureAwait(false);
            collector.Report(result.Diagnostics);

            if (result.Value is not { } manifest)
            {
                continue;
            }

            var source = GraphIds.Relative(root, file);
            var pluginId = collector.Node(GraphNodeType.Plugin, manifest.Name, source);

            if (HostOf(manifest.RepositoryUrl) is { } host)
            {
                collector.Edge(
                    pluginId,
                    collector.Node(GraphNodeType.ExternalHost, host),
                    GraphRelation.UpdatesFrom,
                    GraphConfidence.Declared,
                    new GraphEvidence(source, null, "plugin manifest repository"));
            }
        }

        await AddMarketplacesAsync(root, files, collector, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the marketplace files for where each listed plugin updates from.
    /// </summary>
    /// <remarks>
    /// There is no marketplace node. The approved node types do not include one, and adding a type to the JSON
    /// contract to hold a fact that fits on an existing edge is how a graph becomes a schema nobody can change.
    /// The marketplace file is the *evidence* instead, which is where it belongs.
    /// </remarks>
    private async Task AddMarketplacesAsync(
        string root,
        IReadOnlyList<string> files,
        GraphCollector collector,
        CancellationToken cancellationToken)
    {
        foreach (var file in files.Where(IsMarketplaceManifest))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _manifests.ReadMarketplaceAsync(file, cancellationToken).ConfigureAwait(false);
            collector.Report(result.Diagnostics);

            if (result.Value is not { } listing)
            {
                continue;
            }

            var source = GraphIds.Relative(root, file);

            foreach (var entry in listing.Entries)
            {
                // Only a plugin that is actually here. A listing naming a plugin nobody installed is a fact about
                // the marketplace, and inventing a node for it would put things in the graph that are not present.
                if (!collector.Has(GraphIds.Of(GraphNodeType.Plugin, entry.PluginName))
                    || HostOf(entry.RepositoryUrl) is not { } host)
                {
                    continue;
                }

                collector.Edge(
                    GraphIds.Of(GraphNodeType.Plugin, entry.PluginName),
                    collector.Node(GraphNodeType.ExternalHost, host),
                    GraphRelation.UpdatesFrom,
                    GraphConfidence.Declared,
                    new GraphEvidence(source, null, "marketplace entry repository"));
            }
        }
    }

    /// <summary>Reads every skill, what it ships and what its own text points at.</summary>
    private async Task AddSkillsAsync(
        string root,
        IReadOnlyList<McpServerDeclaration> servers,
        GraphCollector collector,
        CancellationToken cancellationToken)
    {
        foreach (var directory in _discovery.FindSkillDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = GraphIds.Relative(root, directory);
            var load = await _loader.LoadAsync(directory, cancellationToken).ConfigureAwait(false);

            if (load.Value is not { } skill)
            {
                // A skill that will not load is still installed, so it is a node. What it can reach cannot be read
                // without loading it, so it gets no edges — and `validate` is what says why it will not load.
                collector.Report(load.Diagnostics);
                collector.Node(
                    GraphNodeType.Skill,
                    Path.GetFileName(directory.TrimEnd('/', '\\')),
                    source);
                continue;
            }

            var name = skill.Name.Length > 0 ? skill.Name : Path.GetFileName(directory.TrimEnd('/', '\\'));
            var skillId = collector.Node(GraphNodeType.Skill, name, source);
            var skillFile = GraphIds.Relative(root, skill.SkillFilePath);

            AddContainingPlugin(root, directory, skillId, collector);

            var inspection = await _inspector.InspectAsync(skill, cancellationToken).ConfigureAwait(false);

            // The whole entry point, once, so that a claim read out of the frontmatter cites the frontmatter's own
            // line. The loader carries the body and where it starts but not the frontmatter's text, and an
            // `allowed-tools` edge pointing at a line in the prose would be a citation to the wrong place.
            var text = await ReadTextAsync(skill.SkillFilePath, cancellationToken).ConfigureAwait(false)
                ?? skill.Body;

            AddApprovalBoundary(skill, skillId, root, collector);
            AddSkillScripts(root, skill, skillId, collector);
            AddSkillHosts(text, skillFile, skillId, inspection.ExternalUrls, collector);
            AddDeclaredServers(skill, text, skillFile, skillId, servers, collector);
            AddMentionedServers(text, skillFile, skillId, servers, collector);
        }
    }

    /// <summary>
    /// Links a skill to the plugin whose directory it sits inside.
    /// </summary>
    /// <remarks>
    /// Two edges from one reading, because they answer two different questions. <c>Contains</c> is the structure:
    /// this plugin ships this skill. <c>DistributedBy</c> is the supply chain: whoever updates the plugin updates
    /// the skill. A reviewer following an update path wants the second and would have to derive it from the first.
    /// </remarks>
    private void AddContainingPlugin(string root, string skillDirectory, string skillId, GraphCollector collector)
    {
        var directory = Path.GetDirectoryName(skillDirectory.TrimEnd('/', '\\'));

        while (directory is { Length: > 0 } && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(
                directory,
                ProvenanceInspector.PluginMetadataDirectory,
                ProvenanceInspector.PluginManifestFileName);

            if (_fileSystem.FileExists(manifest))
            {
                var source = GraphIds.Relative(root, manifest);

                // The plugin node this manifest produced, which is absent when the manifest would not parse. That
                // is reported by the plugin pass, and nothing is claimed about a file that could not be read.
                if (collector.From(GraphNodeType.Plugin, source)?.Id is { } pluginId)
                {
                    collector.Edge(
                        pluginId,
                        skillId,
                        GraphRelation.Contains,
                        GraphConfidence.Declared,
                        new GraphEvidence(source, null, "skill directory inside plugin"));

                    collector.Edge(
                        skillId,
                        pluginId,
                        GraphRelation.DistributedBy,
                        GraphConfidence.Declared,
                        new GraphEvidence(source, null, "skill directory inside plugin"));
                }

                return;
            }

            directory = Path.GetDirectoryName(directory);
        }
    }

    /// <summary>
    /// Draws a human approval boundary, and only from a field somebody wrote.
    /// </summary>
    /// <remarks>
    /// **The rule this method exists to hold: no boundary without a structured declaration.** The evidence is
    /// <c>approval.required: true</c> in the skill's own <c>skillforge.yaml</c> — a schema field with a boolean
    /// value, read by the same reader that reads the permissions.
    ///
    /// Prose is never evidence here. A <c>SKILL.md</c> that says "always ask the user before deploying" is a
    /// sentence nothing enforces; drawing a boundary from it would put a safety control on a diagram that no code
    /// implements, and a reviewer who trusted the diagram would be worse off than one who had no diagram. There is
    /// a test named for exactly that case.
    ///
    /// <c>required: false</c> draws nothing either. It is a declaration, and a useful one, but it is a declaration
    /// that there is no boundary — and a node for the absence of a thing is not a node.
    /// </remarks>
    private static void AddApprovalBoundary(
        SkillDefinition skill,
        string skillId,
        string root,
        GraphCollector collector)
    {
        if (skill.Configuration.ApprovalRequired is not true)
        {
            return;
        }

        var source = GraphIds.Relative(
            root,
            Path.Combine(skill.DirectoryPath, SkillDefinition.ConfigurationFileName));

        // Named after what it guards, when the declaration says. A boundary with no subject is still a boundary,
        // and naming it after the skill is more honest than inventing a scope for it.
        var subject = skill.Configuration.ApprovalBefore.Count > 0
            ? string.Join(", ", skill.Configuration.ApprovalBefore)
            : skill.Name;

        collector.Edge(
            skillId,
            collector.Node(GraphNodeType.ApprovalBoundary, $"human approval: {subject}", source),
            GraphRelation.ApprovedBy,
            GraphConfidence.Declared,
            new GraphEvidence(source, null, "approval.required in skillforge.yaml"));
    }

    private static void AddSkillScripts(
        string root,
        SkillDefinition skill,
        string skillId,
        GraphCollector collector)
    {
        foreach (var script in skill.Resources.Where(file => file.Kind == SkillResourceKind.Script))
        {
            var path = GraphIds.Relative(root, script.AbsolutePath);

            collector.Edge(
                skillId,
                collector.Node(GraphNodeType.Script, path, path),
                GraphRelation.Contains,
                GraphConfidence.Declared,
                new GraphEvidence(path, null, "executable file in skill directory"));
        }
    }

    private static void AddSkillHosts(
        string text,
        string skillFile,
        string skillId,
        IReadOnlyList<string> urls,
        GraphCollector collector)
    {
        foreach (var url in urls)
        {
            if (HostOf(url) is not { } host)
            {
                continue;
            }

            collector.Edge(
                skillId,
                collector.Node(GraphNodeType.ExternalHost, host),
                GraphRelation.References,
                GraphConfidence.Declared,
                new GraphEvidence(skillFile, LineIn(text, url), "url written in the skill body"));
        }
    }

    /// <summary>
    /// Servers a skill's <c>allowed-tools</c> names, through the <c>mcp__server__tool</c> convention.
    /// </summary>
    /// <remarks>
    /// Structured, so <see cref="GraphConfidence.Declared"/>: the frontmatter is a machine-readable list and the
    /// naming convention is how a tool from an MCP server is spelled in one. This is the strongest evidence a
    /// skill-to-server edge can have without running anything.
    /// </remarks>
    private static void AddDeclaredServers(
        SkillDefinition skill,
        string text,
        string skillFile,
        string skillId,
        IReadOnlyList<McpServerDeclaration> servers,
        GraphCollector collector)
    {
        foreach (var tool in skill.Frontmatter.AllowedTools)
        {
            if (McpServerNameIn(tool) is not { } declared)
            {
                continue;
            }

            var server = servers.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, declared, StringComparison.OrdinalIgnoreCase));

            if (server is null)
            {
                continue;
            }

            collector.Edge(
                skillId,
                GraphIds.Of(GraphNodeType.McpServer, server.Name),
                GraphRelation.Invokes,
                GraphConfidence.Declared,
                new GraphEvidence(skillFile, LineIn(text, tool), "allowed-tools entry"));
        }
    }

    /// <summary>
    /// Servers a skill's body names in prose.
    /// </summary>
    /// <remarks>
    /// <see cref="GraphConfidence.Inferred"/>, and it has to be. A server's name appearing in a skill's text is
    /// exact evidence of a mention and a guess about a call — the skill might be telling the reader not to use it.
    /// The edge is worth having because a reviewer asking "what talks to production?" would otherwise miss every
    /// skill that does not declare its tools, and it is labelled because the alternative is a diagram that presents
    /// somebody's prose as a wiring diagram.
    ///
    /// Names shorter than four characters are skipped: a server called <c>db</c> or <c>ci</c> matches inside
    /// ordinary words, and an edge produced that way is noise with a citation.
    /// </remarks>
    private static void AddMentionedServers(
        string text,
        string skillFile,
        string skillId,
        IReadOnlyList<McpServerDeclaration> servers,
        GraphCollector collector)
    {
        foreach (var server in servers.Where(server => server.Name.Length >= 4))
        {
            if (LineIn(text, server.Name) is not { } line)
            {
                continue;
            }

            collector.Edge(
                skillId,
                GraphIds.Of(GraphNodeType.McpServer, server.Name),
                GraphRelation.Invokes,
                GraphConfidence.Inferred,
                new GraphEvidence(skillFile, line, "server name written in the skill body"));
        }
    }

    /// <summary>Reads the instruction files, and which declared servers each one names.</summary>
    private async Task AddInstructionFilesAsync(
        string root,
        IReadOnlyList<string> files,
        IReadOnlyList<McpServerDeclaration> servers,
        GraphCollector collector,
        CancellationToken cancellationToken)
    {
        foreach (var file in files.Where(IsInstructionFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = GraphIds.Relative(root, file);
            var fileId = collector.Node(GraphNodeType.InstructionFile, path, path);
            var text = await ReadTextAsync(file, cancellationToken).ConfigureAwait(false);

            if (text is null)
            {
                continue;
            }

            foreach (var server in servers.Where(server => server.Name.Length >= 4))
            {
                if (LineIn(text, server.Name) is not { } line)
                {
                    continue;
                }

                collector.Edge(
                    fileId,
                    GraphIds.Of(GraphNodeType.McpServer, server.Name),
                    GraphRelation.References,
                    GraphConfidence.Inferred,
                    new GraphEvidence(path, line, "server name written in the instruction file"));
            }
        }
    }

    /// <summary>
    /// Reads the hook configurations, and which of the tree's scripts each one names.
    /// </summary>
    /// <remarks>
    /// By text search rather than by parsing the hook format. The hook schema belongs to somebody else's product
    /// and a reader for it would be a new seam in Infrastructure for one edge type; a path that appears in the
    /// file, cited to its line, is checkable by whoever reads the report and is honest about being a match rather
    /// than an execution plan. That is why the edge is <see cref="GraphConfidence.Inferred"/>.
    /// </remarks>
    private async Task AddHooksAsync(
        string root,
        IReadOnlyList<string> files,
        GraphCollector collector,
        CancellationToken cancellationToken)
    {
        // Read once, before the loop: the script nodes are complete by now, because the skill pass ran first.
        var scripts = collector.Of(GraphNodeType.Script);

        foreach (var file in files.Where(candidate => IsHookFile(GraphIds.Relative(root, candidate))))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = GraphIds.Relative(root, file);
            var hookId = collector.Node(GraphNodeType.Hook, path, path);
            var text = await ReadTextAsync(file, cancellationToken).ConfigureAwait(false);

            if (text is null)
            {
                continue;
            }

            foreach (var script in scripts)
            {
                if (LineIn(text, script.Name) is not { } line)
                {
                    continue;
                }

                collector.Edge(
                    hookId,
                    script.Id,
                    GraphRelation.Executes,
                    GraphConfidence.Inferred,
                    new GraphEvidence(path, line, "script path written in the hook configuration"));
            }
        }
    }

    /// <summary>
    /// The one-based line where a value appears, or <see langword="null"/> when it does not.
    /// </summary>
    /// <remarks>
    /// The first occurrence. An edge is one claim about two nodes, so one citation is what it needs; a list of
    /// every line a server's name appears on is a search result, not evidence for a relationship.
    /// </remarks>
    private static int? LineIn(string text, string value)
    {
        var lines = text.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return null;
    }

    /// <summary>
    /// A file's text, or <see langword="null"/> when it cannot be read. A file that will not read contributes no
    /// edges and no finding: it is not SkillForge's business why somebody's <c>CLAUDE.md</c> is unreadable, and
    /// claiming an edge from a file that could not be opened would be a claim with no evidence behind it.
    /// </summary>
    private async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await _fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The server named by an <c>mcp__server__tool</c> entry, or <see langword="null"/> when the entry is not one.
    /// </summary>
    private static string? McpServerNameIn(string tool)
    {
        const string prefix = "mcp__";

        if (!tool.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = tool[prefix.Length..];
        var separator = rest.IndexOf("__", StringComparison.Ordinal);

        // `mcp__server` with no tool part still names a server: the whole server is allowed.
        var name = separator > 0 ? rest[..separator] : rest;

        return name.Length > 0 ? name : null;
    }

    private IEnumerable<string> TreeFilePaths(string root) =>
        _fileSystem.EnumerateFiles(root).Where(file => !IsIgnored(root, file));

    private static bool IsIgnored(string root, string file) =>
        GraphIds.Relative(root, file)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));

    private static bool IsMcpFile(string path) =>
        McpFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    private static bool IsInstructionFile(string path) =>
        InstructionFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    private static bool IsPluginManifest(string path) =>
        string.Equals(
            Path.GetFileName(path),
            ProvenanceInspector.PluginManifestFileName,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsMarketplaceManifest(string path) =>
        string.Equals(
            Path.GetFileName(path),
            ProvenanceInspector.MarketplaceFileName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A hook file is one named <c>hooks.json</c>, or any JSON directly inside a <c>hooks</c> directory — the same
    /// rule <c>CapabilitySurfaceScanner</c> applies, so the two agree about what a hook is.
    /// </summary>
    private static bool IsHookFile(string relativePath)
    {
        var segments = relativePath.Split('/');

        return string.Equals(segments[^1], "hooks.json", StringComparison.OrdinalIgnoreCase)
            || (segments.Length > 1
                && string.Equals(segments[^2], "hooks", StringComparison.OrdinalIgnoreCase)
                && relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCredentialShaped(string name) =>
        CredentialWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The host part of a URL, or <see langword="null"/> when the value is not one. A command line is not a URL,
    /// and reporting <c>npx</c> as a host would be the graph inventing network reach.
    /// </summary>
    private static string? HostOf(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host is { Length: > 0 }
            ? uri.Host
            : null;
}
