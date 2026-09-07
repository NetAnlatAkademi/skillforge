# Input — weekly ecosystem update, 2026-09-07

A record of the input that set this phase's scope, condensed from the maintainer's
`SKILLFORGE_WEEKLY_AGENT_TASK_2026-09-07.md`. It is kept here for the same reason as
[inputs-2026-09-06-two-week-update.md](inputs-2026-09-06-two-week-update.md): a decision that changed the roadmap
should be traceable to whatever moved it.

**The claims below are secondhand and unverified here.** They moved the *order* of the roadmap, not the shape of any
rule. Every rule that shipped is justified by what SkillForge can observe on disk or read from a server, not by this
document.

## What the week was said to have changed

1. **Agentic Resource Discovery (ARD) v0.91** appeared as a Proposal — a discovery standard for agent resources.
2. **Registry-driven, on-demand capability discovery** became a shipping pattern: an agent finder over a registry,
   pulling capability metadata when a task needs it rather than at configuration time.
3. **A trust gap opened between discovery metadata and real runtime capability.** A registry's claim about what a
   server exposes is written by a publisher; what it exposes is answered by a process.
4. **Skill → MCP → CLI/API → production** became a recognisable production pipeline shape, with the human approval
   boundary inside it treated as a real architectural concept rather than a UX detail.
5. **Cloud agents began running on several sandbox substrates**, so runtime isolation stopped being a
   Docker-specific idea.

## The product decision it set

> SkillForge should not be a registry or a marketplace. It should verify whether discovery and registry metadata
> matches the runtime capability actually offered — together with policy and provenance.

The new central concept it named: **discovery drift**.

The questions it drew the line with — ARD asks *"what agent resource exists?"*; SkillForge asks *"what was
declared, what actually exists, did they drift, does policy allow it, where did it come from?"*

## What it asked for

Priority order:

```text
P0  MCP tools/list full pagination
P1  Graph foundation
P1  ARD discovery abstraction and adapter
P1  Discovery verification
P2  MCP Registry adapter
P2  Policy integration
P2  Approval boundary graph support
P3  Runtime execution-environment model
```

Six sprints: full tool enumeration (19), the graph foundation with console/JSON/Mermaid output (20), a
provider-neutral remote discovery abstraction with an ARD adapter and a `discover` command (21), declared-versus-
runtime verification for remote HTTP MCP resources (22), an MCP Registry read adapter through the same abstraction
(23), and explicit `ApprovalBoundary` graph support (24).

It also specified a security regression matrix: prompt injection in a registry description, credential-bearing
URLs, oversized and deeply nested JSON, redirect loops, 100k resources, duplicate ids, invalid JSON-LD, unknown
extension namespaces; 100+ tools, multi-page tool lists, cursor loops, duplicate tool names, an extra runtime tool,
a missing declared tool, an unreachable host, a stdio declaration. And an assertion that no environment variable
value, `Authorization` header, URL user-info credential, API key or token reaches any console, JSON, SARIF or
exception output.

## What it forbade

- A hosted SkillForge registry, a public marketplace, a plugin installer.
- ARD auto-install, MCP auto-connect, stdio probing, A2A runtime, workflow runtime, sandbox runtime.
- Vercel, Cursor and Cloudflare integrations; vector search, semantic registry ranking, LLM trust scores, a
  database cache.
- Re-implementing anything that already exists: provenance, `provenance diff`, `update analyze`,
  `identity inspect|diff`, `mcp surface`, the MCP protocol adapter abstraction, the GitHub Action, the SARIF
  foundation, `policy check`, `policy diff`, the eval framework. **Extend the existing abstraction; do not build a
  parallel engine.**
- Inferring an approval boundary from prose, or classifying an environment as production from a host name.
- Publishing a new security diagnostic without evidence, real-input measurement and documented reasoning.

## What was answered differently, and why

- **`GraphConfidence` has two levels, not three.** The input's JSON example showed a skill-to-server `Invokes` edge
  as `Declared` on the strength of a `SKILL.md` line. That is exact evidence for a *mention* and a guess about a
  *call*, so a prose match is `Inferred` here and a structured `allowed-tools` entry is `Declared`. There is no
  `Observed` level at all: `graph` talks to nothing, and a level implying a probe would let a diagram imply a
  request that never happened.
- **`--fail-on-drift` gates `discovery verify`, and `graph` has no gate at all.** The input asked for the graph to
  exit `0`, which it does. It also asked for policy integration, which is here as separation rather than as a new
  rule: the composition rules the graph makes possible are documented as deferred until they can be measured.
- **Endpoint drift is in the drift-kind model with no diagnostic code.** The input listed it as a first-class drift
  type. Establishing it needs the URL that answered after redirects, and the MCP prober reports what a server said
  rather than where the socket ended up. A drift kind that cannot be established would be an empty promise, so
  nothing emits it and no code was published.
- **Version drift shipped in its place**, because it is genuinely comparable: a registry's `version` against a
  server's own `serverInfo.version`. It is reported as two claims disagreeing rather than one correcting the other,
  since the specification states plainly that `serverInfo` is not verified by the protocol.
- **`ApprovalBoundary` needed a structured declaration to exist before it could be read.** The input forbade
  inferring one from prose and did not say where explicit evidence would come from. `approval.required` was added
  to `skillforge.yaml`, read by the existing reader, so that every boundary on a diagram traces to a field somebody
  wrote.
- **A registry's URL is sent as given.** The input's `--registry <url>` implies a query grammar SkillForge does not
  know for a proposal-stage format. So a search parameter is appended only when the supplied URL carries no query
  of its own, and the ARD adapter is documented as tolerant precisely because the format will move.
- **`SF1016` reports a registry that could not be read**, in the `SF10xx` block where every other "SkillForge could
  not read this" code lives (`SF1012`, `SF1014`, `SF1015`), rather than in the drift block. A reading failure and a
  drift finding are different kinds of statement.

## Where the work landed

`CHANGELOG.md` under `26.250.1`, `TODO.md` under v0.8, and two new documents: [graph.md](graph.md) and
[discovery.md](discovery.md).
