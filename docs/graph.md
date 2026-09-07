# The asset graph

`skillforge graph` reads a directory and draws what is wired to what: which skills invoke which MCP servers, which
servers reach which hosts, which credential names they read, and which plugin distributes which skill.

```bash
skillforge graph .
skillforge graph . --format json
skillforge graph . --format mermaid
```

It exits `0`. **A graph is not a finding.**

---

## The rule the whole feature is built on

> No evidence, no claim.

Every edge carries the file it was read from and, when the claim was read out of the file's *text* rather than out
of its existence, the line. An edge that cannot say that is not emitted at all.

```json
{
  "source": "skill:deploy-skill",
  "target": "mcp:production-mcp",
  "relation": "Invokes",
  "confidence": "Declared",
  "evidence": {
    "file": "skills/deploy-skill/SKILL.md",
    "line": 5,
    "kind": "allowed-tools entry"
  }
}
```

That is what makes the graph checkable rather than merely plausible. A reader can open the file and disagree.

There is **no transitive closure**. If a skill invokes a server and the server reaches a host, the graph says
exactly those two things and leaves the reader to follow the two hops. An edge SkillForge inferred from other edges
would look, on the diagram, exactly like one it read.

---

## Declared and inferred

Two confidence levels, and the line between them is whether a machine wrote the claim down or a person's prose
implied it.

| Confidence | Read from | Example |
|---|---|---|
| `Declared` | A structured field, a manifest, a file's location | `allowed-tools: [mcp__production-mcp__deploy]` |
| `Inferred` | A name matched in prose, or the spelling of a variable | `production-mcp` appearing in a `SKILL.md` paragraph |

An inferred edge has **exact evidence and a heuristic conclusion**. A server's name in a skill's body proves a
mention; it does not prove a call — the skill might be telling the reader *not* to use it. The edge is worth having
because a reviewer asking "what talks to production?" would otherwise miss every skill that does not declare its
tools, and it is labelled because the alternative is a diagram that presents somebody's prose as a wiring diagram.

Where both readings exist for one pair, **the declared one wins and the inferred one is dropped.** Both are true,
the stronger evidence is the one worth printing, and two edges for one relationship would double every count a
reader takes off the diagram.

There is no third, "observed at runtime" level. `graph` talks to nothing — see [What it does not do](#what-it-does-not-do).

**Server names shorter than four characters are never matched in prose.** A server called `db` or `ci` matches
inside ordinary English words, and an edge produced that way is noise with a citation attached.

---

## Node types

Detected today:

| Type | What it is | Found by |
|---|---|---|
| `Skill` | A directory with a `SKILL.md` | The same discovery `validate` uses |
| `Plugin` | A directory with `.claude-plugin/plugin.json` | The same manifest reader `provenance` uses |
| `McpServer` | A server some `.mcp.json` declares | The same format readers `mcp inspect` uses |
| `InstructionFile` | `CLAUDE.md`, `AGENTS.md`, `copilot-instructions.md` | The names the provider adapters use |
| `Hook` | `hooks.json`, or JSON inside a `hooks/` directory | The same rule `update analyze` applies |
| `Script` | An executable file inside a skill | The same classifier `inspect` uses |
| `ExternalHost` | A host something points at | URL parsing; a command line is not a URL |
| `CredentialSource` | An environment variable or header **name** | The same name heuristic `update analyze` uses |
| `Identity` | How a server is authorised against | The same inference `identity inspect` publishes |
| `ApprovalBoundary` | A declared human decision point | `approval.required` in `skillforge.yaml` |

Named in the model, with **no detector**, so that the JSON contract does not have to grow a value later:
`Workflow`, `Harness`, `Automation`, `ExecutionEnvironment`. A type with no detector produces no nodes, which is
visible in a report as an absence rather than as an empty section.

### Nothing is parsed twice

Every node above comes from the reader that already answers that question elsewhere. That is not tidiness: two
scanners would eventually disagree about a skill, and the one people would believe is whichever they ran last. The
graph's own contribution is the edges.

---

## Edge types

| Relation | Meaning | Typical evidence |
|---|---|---|
| `Contains` | The source holds the target | A script's location inside a skill directory |
| `References` | The source's text names the target | A URL in a skill body |
| `Invokes` | The source calls the target | An `allowed-tools` entry, or a name in prose |
| `ConnectsTo` | The source opens a connection | An MCP server's `url` |
| `ReadsCredential` | The source reads a credential from the target | A declared environment variable name |
| `UsesIdentity` | The source authenticates as the target | Names in the declaration |
| `Executes` | The source runs the target as a process | A script path written in a hook configuration |
| `Declares` | The source declares the target exists | Reserved |
| `DistributedBy` | The source is shipped by the target | A skill directory inside a plugin |
| `UpdatesFrom` | The source takes updates from the target | A plugin manifest's repository |
| `ApprovedBy` | The source needs the target's approval | `approval.required: true` |
| `RunsWithin` | The source executes inside the target | Reserved, with `ExecutionEnvironment` |

`DistributedBy` sits beside `Contains` rather than being derived from it, because they answer different questions.
`Contains` is structure: this plugin ships this skill. `DistributedBy` is supply chain: whoever updates the plugin
updates the skill. A reviewer following an update path wants the second.

---

## Approval boundaries

`ApprovalBoundary` nodes come from **one** place: `approval.required: true` in a skill's own `skillforge.yaml`.

```yaml
# skills/deploy-skill/skillforge.yaml
approval:
  required: true
  before:
    - production deploy
```

```text
deploy-skill --ApprovedBy--> human approval: production deploy
    declared · approval.required in skillforge.yaml · skills/deploy-skill/skillforge.yaml
```

**Prose is never evidence for a boundary.** A `SKILL.md` that says "always ask the user before deploying to
production" is a sentence nothing enforces. Drawing a boundary from it would put a safety control on a diagram that
no code implements, and a reviewer who trusted the diagram would be worse off than one who had no diagram. There is
a test named for exactly that sentence.

`required: false` draws nothing either. It is a declaration, and a useful one, but it declares that there is no
boundary — and a node for the absence of a thing is not a node.

### Production classification, and why there is none

A host called `prod.company.com` is **not** classified as production. That is the name of a host, chosen by a
person, and a graph that read intent out of it would be putting a claim on the diagram that came from a string.

`ExecutionEnvironment` is in the model for when explicit metadata exists to read. Until then, unknown stays
unknown.

### The composition rule that is documented and not implemented

The interesting question this graph makes possible:

```text
ProductionImpactingPath + NoExplicitApprovalBoundary
```

That is **not a finding, and is not published.** It has to be measured on real repositories first, and the
measurement cannot be done honestly while approval boundaries are declared by almost nobody and production is not
classifiable at all. Publishing it now would produce a finding on approximately every repository, which is the
SF1009 failure mode: true, unwanted, and quickly ignored.

---

## Mermaid output

```bash
skillforge graph . --format mermaid
```

```mermaid
graph LR
  credential_deploy_token_635b(["DEPLOY_TOKEN"])
  host_prod_company_com_e520("prod.company.com")
  mcp_production_mcp_6411["production-mcp"]
  skill_deploy_skill_94a7["deploy-skill"]

  mcp_production_mcp_6411 -.->|"ReadsCredential (inferred)"| credential_deploy_token_635b
  mcp_production_mcp_6411 -->|"ConnectsTo"| host_prod_company_com_e520
  skill_deploy_skill_94a7 -->|"Invokes"| mcp_production_mcp_6411
```

A solid arrow is a declared edge; a dotted one is inferred, and says so in the label too — a diagram is read at a
glance and printed in black and white.

Three properties the renderer exists to guarantee:

- **Deterministic identifiers.** A Mermaid identifier may not hold the characters that appear in real server names,
  hosts and paths, so each graph id is transliterated. The transliteration is a pure function of the id — no
  counters, no dictionary order — and where two different ids would collapse to the same identifier, a four-character
  FNV-1a hash of the original keeps them apart. Written out rather than taken from `GetHashCode()`, which is
  randomised per process and would give a different diagram on every run.
- **Safe labels.** Quoting alone is not enough: a quotation mark inside a quoted label ends it, and `#` starts an
  HTML entity. Both are replaced rather than escaped, because a name that needs escaping will be misread by one
  viewer or another.
- **Names, never values.** A credential node's label is the variable's or header's name.

Mermaid is offered because it renders in a pull request without a toolchain. A reviewer looking at a change to
`.mcp.json` can see what it rewired without installing anything.

---

## Secret safety

**No value that could be a credential is read.** Credential nodes are environment variable and header *names*,
which is all the MCP readers ever put in the model in the first place — the values are dropped at parse time rather
than filtered on the way out.

A credential inside a URL — `https://svc:sk-live-abc@db.example.com/mcp` — is stripped where the configuration is
read, so no printer has to remember to filter it. A host node carries the host and nothing else.

There is a test that writes a real-looking token into a `.mcp.json`, as an environment value, as an `Authorization`
header and inside the server URL, and asserts none of them appears in **any** of the three output formats while
`DEPLOY_TOKEN`, `Authorization` and the host name appear in all of them. A test that asserted only absence would
pass on a report that printed nothing at all.

---

## What it does not do

- **It makes no network request.** No `--probe` option exists, and no protocol adapter is injected into the builder.
  Asking a server what it exposes is `mcp surface`'s and `discovery verify`'s business; a diagram that could
  silently make requests would be the one output nobody would think to check for them.
- **It reaches no verdict.** No `--fail-on-*` flag, because a flag needs a rule behind it and the composition rules
  this graph makes possible have not been measured.
- **It emits no SARIF.** SARIF describes results a scanner produced. Uploading a description as static-analysis
  results would put a hundred "issues" on a pull request that nobody claimed were problems.
- **It follows nothing.** A configuration file that will not parse is reported (`SF1015`) so that a missing node is
  a stated gap rather than a silent one, and the run fails on it — a node missing because a file would not read is
  worse than a stated gap, and worst of all when it exits zero.

---

## Determinism

Nodes are ordered by id; edges by source, then target, then relation. Two runs over unchanged input produce
byte-identical output in every format, which is what makes a graph reviewable in a pull request. There is a test
that runs the whole thing twice, in all three formats, and compares the strings.

---

## See also

- [discovery.md](discovery.md) — the runtime half: what a server actually exposes
- [validation-rules.md](validation-rules.md) — the codes a graph reports about its own reading
- [cli-reference.md](cli-reference.md#graph) — every option
