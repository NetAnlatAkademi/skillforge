using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace SkillForge.Infrastructure.Tests.Mcp;

/// <summary>
/// An in-memory MCP server that speaks <c>server/discover</c> and the skills extension, shaped by the test.
/// </summary>
/// <remarks>
/// Every response is built from the specification's field names, so a test that passes here is a test against the
/// shapes a real server sends — and every request is recorded, so a test can assert what was <em>not</em> asked.
/// </remarks>
internal sealed class FakeSkillsServer : HttpMessageHandler
{
    internal const string Url = "https://skills.example.test/mcp";

    internal bool DeclaresSkills { get; set; } = true;

    internal bool DirectoryRead { get; set; }

    internal string Revision { get; set; } = "2026-07-28";

    /// <summary>The <c>skills/list</c> pages, in order. Defaults to one page holding every registered skill.</summary>
    internal List<(JsonArray Skills, string? Next)>? ListPages { get; set; }

    /// <summary>When set, <c>skills/list</c> never ends: every page hands out a fresh cursor.</summary>
    internal bool EndlessList { get; set; }

    /// <summary>What <c>skills/get</c> returns, by skill URI.</summary>
    internal Dictionary<string, JsonObject> Skills { get; } = new(StringComparer.Ordinal);

    /// <summary>Which skills <c>skills/list</c> carries when <see cref="ListPages"/> is not set.</summary>
    internal HashSet<string> Listed { get; } = new(StringComparer.Ordinal);

    /// <summary>What <c>resources/read</c> returns, by URI: the <c>contents</c> item.</summary>
    internal Dictionary<string, JsonObject> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>What <c>resources/directory/read</c> returns, by directory URI.</summary>
    internal Dictionary<string, List<(string Uri, bool IsDirectory)>> Directories { get; } = new(StringComparer.Ordinal);

    /// <summary>Called before each answer; a test uses it to cancel, or to stall.</summary>
    internal Func<string, CancellationToken, Task>? BeforeAnswer { get; set; }

    /// <summary>Every request, as <c>method uri</c>.</summary>
    internal List<string> Requests { get; } = [];

    internal static string Digest(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static string SkillFile(string name, string description, string extra = "") =>
        $"---\nname: {name}\ndescription: {description}\n{extra}---\n\n# {name}\n\nDo the thing.\n";

    /// <summary>
    /// Registers a static skill: its files are served, and its manifest names each with its true digest and size.
    /// </summary>
    internal JsonObject AddStaticSkill(string root, string name, string description, params (string Path, string Text)[] files)
    {
        var all = new List<(string Path, string Text)> { ("SKILL.md", SkillFile(name, description)) };
        all.AddRange(files);

        var resources = new JsonArray();

        foreach (var (path, text) in all)
        {
            var uri = $"{root}/{path}";
            var bytes = Encoding.UTF8.GetBytes(text);

            Files[uri] = new JsonObject { ["uri"] = uri, ["mimeType"] = "text/markdown", ["text"] = text };
            resources.Add(new JsonObject { ["uri"] = uri, ["digest"] = Digest(bytes), ["size"] = bytes.Length });
        }

        return Register(root, name, description, resources);
    }

    internal JsonObject AddDynamicSkill(string root, string name, string description) =>
        Register(root, name, description, JsonValue.Create("dynamic"));

    internal JsonObject Register(string root, string name, string description, JsonNode resources)
    {
        var skill = new JsonObject
        {
            ["uri"] = $"{root}/SKILL.md",
            ["frontmatter"] = new JsonObject { ["name"] = name, ["description"] = description },
            ["resources"] = resources,
        };

        Skills[$"{root}/SKILL.md"] = skill;
        Listed.Add($"{root}/SKILL.md");

        return skill;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(CancellationToken.None))!;
        var method = body["method"]!.GetValue<string>();
        var parameters = body["params"] as JsonObject ?? [];
        var uri = parameters["uri"]?.GetValue<string>();

        Requests.Add(uri is null ? method : $"{method} {uri}");

        if (BeforeAnswer is { } hook)
        {
            await hook(method, cancellationToken);
        }

        return method switch
        {
            "server/discover" => Result(Discover()),
            "skills/list" => Result(List(parameters["cursor"]?.GetValue<string>())),
            "skills/get" => Skills.TryGetValue(uri!, out var skill)
                ? Result(new JsonObject
                {
                    ["resultType"] = "complete",
                    ["skill"] = skill.DeepClone(),
                    ["ttlMs"] = 300000,
                    ["cacheScope"] = "public",
                })
                : Error(-32602, $"No skill is served at {uri}"),
            "resources/read" => Files.TryGetValue(uri!, out var item)
                ? Result(new JsonObject { ["contents"] = new JsonArray(item.DeepClone()) })
                : Error(-32602, $"No resource at {uri}"),
            "resources/directory/read" => Directories.TryGetValue(uri!, out var children)
                ? Result(new JsonObject
                {
                    ["resources"] = new JsonArray([.. children.Select(child => (JsonNode)new JsonObject
                    {
                        ["uri"] = child.Uri,
                        ["mimeType"] = child.IsDirectory ? "inode/directory" : "text/markdown",
                    })]),
                })
                : Error(-32602, $"No directory at {uri}"),
            _ => Error(-32601, "method not found"),
        };
    }

    private JsonObject Discover()
    {
        var capabilities = new JsonObject { ["resources"] = new JsonObject() };

        if (DeclaresSkills)
        {
            capabilities["extensions"] = new JsonObject
            {
                ["io.modelcontextprotocol/skills"] = new JsonObject { ["directoryRead"] = DirectoryRead },
            };
        }

        return new JsonObject
        {
            ["supportedVersions"] = new JsonArray(Revision),
            ["capabilities"] = capabilities,
            ["_meta"] = new JsonObject
            {
                ["io.modelcontextprotocol/serverInfo"] = new JsonObject { ["name"] = "fake-skills", ["version"] = "1.2.3" },
            },
        };
    }

    private JsonObject List(string? cursor)
    {
        if (EndlessList)
        {
            var page = cursor is null ? 1 : int.Parse(cursor["c".Length..], System.Globalization.CultureInfo.InvariantCulture) + 1;

            return new JsonObject
            {
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["uri"] = $"skill://endless/s{page}/SKILL.md",
                    ["frontmatter"] = new JsonObject { ["name"] = $"s{page}", ["description"] = "x" },
                    ["resources"] = "dynamic",
                }),
                ["nextCursor"] = $"c{page}",
            };
        }

        var pages = ListPages ?? [(new JsonArray([.. Listed.Order(StringComparer.Ordinal).Select(uri => Skills[uri].DeepClone())]), null)];
        var index = cursor is null ? 0 : int.Parse(cursor["page".Length..], System.Globalization.CultureInfo.InvariantCulture);
        var (skills, next) = pages[index];

        var result = new JsonObject
        {
            ["resultType"] = "complete",
            ["skills"] = skills.DeepClone(),
            ["ttlMs"] = 300000,
            ["cacheScope"] = "public",
        };

        if (next is not null)
        {
            result["nextCursor"] = next;
        }

        return result;
    }

    private static HttpResponseMessage Result(JsonObject result) =>
        Json(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = "1", ["result"] = result });

    private static HttpResponseMessage Error(int code, string message) =>
        Json(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "1",
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        });

    private static HttpResponseMessage Json(JsonObject payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
}
