# Skills served over MCP

`skillforge mcp skills inspect|verify` reads skills a server serves through the `io.modelcontextprotocol/skills`
extension and checks that what arrives is what the server's manifest described.

```bash
skillforge mcp skills inspect ./.mcp.json --probe
skillforge mcp skills verify  ./.mcp.json --probe --fail-on-mismatch --format sarif -o skills.sarif
```

Options are in [cli-reference.md](cli-reference.md#mcp-skills); codes SF8301–SF8308 in
[validation-rules.md](validation-rules.md#mcp-skills).

## Transport, not format

MCP Skills is a **transport**. Agent Skills is the **content format**. SkillForge already reads the format, so the
extension adds no second skill parser:

```text
server/discover          (the existing prober: does the server declare the extension, and directoryRead?)
    ↓
skills/list              (bounded walk shared with tools/list)
skills/get               (the manifest: uri, frontmatter, resources, ttlMs, cacheScope)
resources/read           (raw bytes of each manifest file)
resources/directory/read (only when declared; compared against the manifest, never added to it)
    ↓
materialised entry
    ↓
the existing frontmatter parser, SHA-256 calculator and provenance model
```

The frontmatter `skills/get` describes is a JSON object. JSON is valid YAML, so the **same** `IFrontmatterParser` that
reads `SKILL.md` on disk reads it — and the frontmatter split out of the served `SKILL.md` goes through it too. Two
frontmatters that say the same thing come out as the same model, which is what makes the field comparison exact.

Every request goes through one JSON-RPC channel, `McpJsonRpcChannel`, which `tools/list` now uses as well. Every
paginated method is walked by one `McpCursorWalk`. A server cannot find a list that one method bounds and another
does not.

## What is asked, and when

| Condition | What happens |
|---|---|
| No `--probe` | Nothing leaves the machine. Each server reports that `--probe` was not given |
| stdio server | Never launched, never asked |
| Discovery not answered, or answered in a revision other than `2026-07-28` | Not asked about skills, and the reason is printed |
| Extension not declared | Not asked about skills |
| Extension declared | `skills/list`; under `verify`, `skills/get` and `resources/read` for each skill |
| `directoryRead: true` | Under `verify`, `resources/directory/read` on the skill root and its subdirectories |

A setting counts as enabled only when it is the JSON literal `true`; the string `"true"` is not a boolean.

## Integrity, and only integrity

For a static manifest:

```text
skills/get → manifest → resources/read → actual bytes → SHA-256 + length → compare
```

| Check | Code on failure |
|---|---|
| Digest algorithm is `sha256` and the value is 64 lowercase hex characters | SF8306 |
| Every file declares a size; no file is listed twice; the skill's own `SKILL.md` is listed | SF8306 |
| At most 512 files and 16 MiB declared — the specification's per-skill limits | SF8306 |
| Every listed file can be read | SF8305 |
| Its byte length is the declared size | SF8302 |
| Its SHA-256 is the declared digest | SF8301 |
| The `SKILL.md` frontmatter equals the described frontmatter, field by field | SF8303 |
| Every file the directory listing shows is in the manifest | SF8304 |

A malformed manifest stops everything before a byte is read: the specification says a host must not load on one, and
verifying against a baseline nobody defined would be verifying nothing.

**`IntegrityMatch` is the only success word, and it means only this:** the bytes that arrived are the bytes the
manifest described. It does not mean trusted, safe, secure, or a verified publisher. The digests are unsigned and come
from the same server as the content; a server that rewrites a file and its digest together stays consistent. The
specification says digests are not a security boundary, and SkillForge keeps five things apart:

```text
Integrity   — what this command establishes
Provenance  — where the skill came from (recorded below; not verified)
Trust       — not established by anything here
Policy      — 'policy check', a different command
Behaviour   — what the skill does when run; nothing here runs it
```

## Dynamic skills

`"resources": "dynamic"` is a form the specification defines for generated content. It is not invalid. No file is
read, no hash baseline is fabricated, and the report says, as SF8307: *Static content integrity cannot be
established.*

## The manifest is the approved set

A file the directory listing shows that the manifest does not name is **unlisted** (SF8304). It is reported, not read,
and never silently added to the approved set. Only children inside the skill's own subtree count; another skill's
files, another scheme, and paths with `.` or `..` segments are ignored rather than followed. Without `directoryRead`
the report says `directory: not declared`, and "no unlisted files" is not a claim it makes.

## Provenance of an MCP-served skill

Each verified skill carries a provenance record with origin `McpServer`:

| Field | Source |
|---|---|
| `endpoint` | The URL the configuration named, with any user-info removed |
| `selfReportedName`, `selfReportedVersion` | `serverInfo` from `server/discover` — self-reported |
| `protocolVersion` | The revision the server answered in |
| `skillUri` | The skill's `SKILL.md` URI |
| `manifest` | Each file's URI, declared digest and declared size |
| `observedAt` | When the manifest was read |
| `serverIdentityVerified` | Always `false`: nothing in the protocol establishes it |

## Bounds

| Bound | Value |
|---|---|
| Pages per paginated walk | 100, plus a stop on a repeated cursor and a cancellation check before every request |
| Response body | 24 MiB, refused rather than truncated (a 16 MiB file base64-encoded, plus envelope) |
| JSON nesting | 64 levels |
| Request timeout | 30 seconds, covering the response body as well as the headers |
| Skills verified per server | 100 |
| Directories walked per skill | 64, eight pages each, at most 1,024 entries kept |

## Known limits

- **Text content and the digest.** `resources/read` returns a text file as a decoded string. SkillForge re-encodes it
  as UTF-8 to hash it. A file the server stores in another encoding, or with a byte order mark, does not survive that
  round trip and is reported as a mismatch. A server that wants byte-exact verification of such a file should serve
  it as a `blob`.
- **Comparable frontmatter fields.** The comparison covers the fields SkillForge models: `name`, `description`,
  `license`, `compatibility`, `allowed-tools`, and the scalar entries of `metadata`. Any other author field is not
  compared.
- **Manifest files outside the skill's root.** The specification does not require a manifest URI to sit under the
  skill's root, so SkillForge reads every file the manifest names, from the same server, and reports what it finds.
  A manifest naming another skill's file is not, today, a finding.
- **Directory children outside the subtree** — another skill, another scheme, an empty, `.` or `..` segment — are not
  followed, and their presence marks the enumeration `partial`.
- **Credentials.** SkillForge never reads header values from a configuration, so a server that requires
  authorization is reported as such and its skills are not read.
- **Prevalence.** The codes were measured on fixtures (see validation-rules.md). No real server implementing the
  extension was available to measure against.

## Specification

The extension's specification is published at `specification/beta/skills.mdx` in
`modelcontextprotocol/ext-skills` — **beta**, not stable, at the time of writing. The brief this was built from named a
`stable/` path that does not exist. Field names here (`skills`, `skill`, `uri`, `frontmatter`, `resources`, `digest`,
`size`, `ttlMs`, `cacheScope`, `directoryRead`, `inode/directory`) are copied from that document. If the stable
revision renames any of them, the change is confined to `Mcp20260728SkillsClient`.
