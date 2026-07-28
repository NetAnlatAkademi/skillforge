# Validation rules

Every rule owns a stable diagnostic code. Codes are never reused or renumbered once released.

- `SF0xxx` — **Error**. The skill is not usable as written.
- `SF1xxx` — **Warning**. The skill works but quality or risk deserves attention.
- `SF2xxx` — **Info**. A neutral observation about the skill's surface.

Four further bands cover the risk work after v0.1.0 (roadmap §30): `SF3xxx` activation and retrieval risks,
`SF4xxx` instruction injection, `SF5xxx` supply chain and provenance, `SF6xxx` version and evolution. `SF3xxx`
and `SF4xxx` have rules; `SF5xxx` and `SF6xxx` are still reserved.

**Signals, not verdicts.** The permission and shell rules point things out; they never conclude that a skill is
unsafe (ADR-006). Every construct SF1007 recognises has legitimate uses — a build script may well need `sudo`, and
`rm -rf` on a temporary directory is housekeeping. What they have in common is that somebody deciding whether to
trust a skill would want to know they are there, and nobody reads every script by hand.

Through v0.1.0 the set was deliberately closed at 24 codes, which is why an unreadable `SKILL.md` widened
SF0001 rather than getting a new code. Those bands lift that constraint on purpose. The rule that does **not**
change: a published code's meaning and severity are fixed. Adding a code is cheap; redefining one breaks every
CI configuration that suppresses it.

The `Status` column tracks implementation, so the table doubles as a checklist. `Planned` means the
code is reserved but no rule exists yet.

Codes marked *(loader)* are produced while reading the skill rather than by a validation rule. The
loader reports only what prevents a skill from being modelled at all; everything else — including a
missing `name` or `description` — is a rule that runs on the loaded model.

When one mistake would trigger two codes, the more precise one wins. A duplicated frontmatter field
also makes the YAML parser fail, so SF0009 is reported and SF0003 is suppressed.

SF0001 covers both "not found" and "found but unreadable" — a locked file or a permission error reads the same
way to the person running the CLI. That widening was made when the code set was still deliberately closed; the
set is open now, but SF0001 keeps its meaning, because changing a published code is the thing that stays
forbidden.

Two codes have a defined precedence so one mistake produces one finding:

- SF0004 suppresses SF0006 and SF0007 has nothing to say about it — a skill with no name is not also
  reported as having an invalid one.
- SF0005 suppresses SF1001 and SF1002 for the same reason.
- SF0008 and SF1011 both suppress SF0007 for a given reference: a link that leaves the skill is an escape, not a
  missing file, and this rule cannot look outside the skill's own inventory anyway.
- SF0008 and SF1011 are mutually exclusive by construction — a reference either reaches a sibling or reaches
  further, never both.

## Thresholds and heuristics

Rules that judge rather than check state their bar here, so a disagreement can be argued with the
number in front of it.

| Rule | Bar | Why |
|---|---|---|
| SF0006 | 2–64 characters, `^[a-z][a-z0-9]*(-[a-z0-9]+)*$` | The name becomes a package file name, a directory name and a command line argument on three operating systems. |
| SF0007 | Case-sensitive comparison on every platform | `References/Notes.md` for `references/notes.md` works on Windows and breaks on Linux. Reporting it everywhere names the portability bug up front. |
| SF0010 | Semantic versioning | Consumers need to be able to compare two versions. A version is optional; only a malformed one is an error. |
| SF1001 | 40 characters | An agent choosing between skills has only the description. "Reviews APIs." does not distinguish this one from ten others. |
| SF1002 | Mentions *when, whenever, while, during, before, after, if* | A deliberate heuristic. A description can state its trigger without those words, which is why this is a warning the author may ignore. |
| SF1003 | 500 lines | A long entry point means reference material should live in its own file that the agent reads only when needed. |
| SF1005 | Only when a declaration contradicts the content | "A URL is present" fires on 60 of 203 real skills and says nothing; `inspect` reports URLs as an observation instead. A skill that declares `network.allowed: false` and then names a host has one of the two wrong. |
| SF1006 | Any script, unless shell permission is declared | Measured: 7 of 203 real skills ship a script, so this speaks up about a few percent rather than everything. |
| SF1007 | Seven literal shell patterns, one finding per pattern per file | Deliberately literal. A pattern that guesses at intent misses the obfuscated case and cries wolf about the ordinary one. |

## Errors

| Code | Rule | Status |
|---|---|---|
| SF0001 | `SKILL.md` was not found, or exists but could not be read | **Implemented** (loader) |
| SF0002 | YAML frontmatter was not found | **Implemented** (loader) |
| SF0003 | YAML frontmatter could not be parsed | **Implemented** (loader) |
| SF0004 | `name` field is missing | **Implemented** |
| SF0005 | `description` field is missing | **Implemented** |
| SF0006 | Skill name is invalid | **Implemented** |
| SF0007 | A referenced file was not found | **Implemented** |
| SF0008 | A path reaches outside the skill and its neighbours | **Implemented** (loader + rule) |
| SF0009 | The same metadata field is declared more than once | **Implemented** (loader) |
| SF0010 | Package version is invalid | **Implemented** |

## Warnings

| Code | Rule | Status |
|---|---|---|
| SF1001 | Description is too short | **Implemented** |
| SF1002 | Description does not state an activation context | **Implemented** |
| SF1003 | `SKILL.md` is longer than 500 lines | **Implemented** |
| SF1004 | An unused file is present | Planned |
| SF1005 | The skill points at a host but declares `network.allowed: false` | **Implemented** |
| SF1006 | A script ships but no shell permission is declared | **Implemented** |
| SF1007 | A script uses a construct that reaches further than usual | **Implemented** |
| SF1008 | Package dependencies are not pinned | Planned |
| SF1009 | No license is declared | **Implemented** |
| SF1010 | No agent compatibility information is declared | **Implemented** |
| SF1011 | A reference points at a sibling skill, outside this skill's own directory | **Implemented** |
| SF1012 | `skillforge.yaml` exists but could not be parsed, so its settings were ignored | **Implemented** |
| SF1013 | A `version` field was written at the top level instead of under `metadata` | **Implemented** (loader) |
| SF1014 | A file under `evals` could not be read or parsed, so its cases were skipped | **Implemented** (`eval`) |
| SF1015 | A provider's own configuration file could not be read, so what it declares is missing from the migration inventory | **Implemented** (`migrate inspect`) |

SF1013 exists because the old behaviour was to discard the value without a word. Every other field a skill declares
is top-level, so writing `version:` there is an easy mistake — and it meant SF0010 never checked the version,
`inspect`, `pack` and `diff` reported none, and SF6001 could not fire at all. It was found by writing a test fixture
that way and believing the tool.

Two responses were available and neither alone was right. Accepting it silently leaves the schema permanently
ambiguous. Reporting it without reading it leaves the author's value unusable while telling them off. So SkillForge
does both: it reads the value, and it says where the value belongs. An explicit `metadata.version` wins if both are
present — the author has said the same thing twice, and the schema decides which one to believe.

## Info

| Code | Rule | Status |
|---|---|---|
| SF2001 | The skill contains a script | **Implemented** (inspect) |
| SF2002 | The skill contains an external URL | **Implemented** (inspect) |
| SF2003 | The skill contains a binary file | **Implemented** (inspect) |
| SF2004 | The skill contains an `evals` folder | **Implemented** (inspect) |

## Activation and retrieval risks

| Code | Rule | Status |
|---|---|---|
| SF3001 | The description claims the skill applies always, or to everything | **Implemented** |
| SF3002 | The skill's text pushes an agent to prefer it over its other instructions | **Implemented** |

**These two were validated differently from every other rule, and the difference matters.** Measured across 203
real skill descriptions, each pattern fires on at most one skill. For a quality rule that would be grounds not to
ship it. For these it is the goal: a skill telling an agent to ignore its other instructions is what an attacker
writes, and attackers are not in a sample of benign skills. Measuring benign input proves the **absence of false
positives**; it cannot prove the presence of value. That is demonstrated with deliberately crafted positives in the
tests instead.

SF3001 is the mirror of SF1002. That rule asks whether a description says *when* the skill applies; this one asks
whether it says "whenever", which is the same failure wearing confidence.

**Both read the description only, and SF3002 had to be cut back to get there.** It first read the body as well, on the
reasoning that an instruction to disregard other instructions does its work wherever the agent reads it. Measured on
229 real skills that produced 16 SF3xxx findings across 14 skills, and inspecting the matched lines showed roughly one
genuine hit. The rest were ordinary English in ordinary prose — "say so instead of hiding behind tooling",
"# Ignore other fields", and a security skill's own detection pattern written as a string literal. Two changes
followed: the body is no longer read, and the weakest pattern (`instead of` / `rather than`, 8 findings, every one
benign) was deleted. Re-measured on the same 229 skills: **5 findings, 4 of them SF3001 and 1 SF3002**, all
defensible. Finding injected instructions inside a *body* is a different problem and belongs to the reserved `SF4xxx`
band, not to an approximation here at a 90% false-positive rate.

| Code | After the cut, on 229 real skills |
|---|---|
| SF3001 | 4 — `using-superpowers`, `verification-before-completion`, `vgen-pr`, `vgen-refactor` |
| SF3002 | 1 — `using-superpowers` ("invoke skills BEFORE any response") |

Neither concludes anything. The message says what was recognised, and the suggestion says outright that SkillForge
is not calling the skill malicious (ADR-006) — a legitimate skill can be written clumsily, and a reader with the
finding in front of them judges better than a regex.

## Instruction injection in the body

| Code | Rule | Status |
|---|---|---|
| SF4001 | The body's prose tells the agent to set aside or override its instructions | **Implemented** |
| SF4002 | The body's prose tells the agent to keep something from the user | **Implemented** |

This band exists because SF3002 was measured out of the job. It scanned whole bodies with loose patterns and
produced twelve findings across 229 real skills, of which roughly one was real. Crucially, the failures were not
ambiguous English — they were **code being shown to a reader**: a YAML comment reading `# Ignore other fields`,
and a security skill's own detection pattern written as the string literal `r'ignore (previous|all) instructions'`.

So these rules were built with two independent defences, either of which would have caught one of those two, and
which together catch both:

1. **They read prose, not text.** `MarkdownProse` drops fenced code blocks and removes inline code spans before
   any pattern runs. Nothing else is filtered — indented blocks are kept, because no measurement justified
   dropping them and guessing would trade a known false-positive class for an unknown false-negative one.
2. **Every pattern requires the noun it is about.** SF3002 matched `ignore … other`; SF4001 requires
   `ignore … other instructions`. Ignoring *fields*, *files* or *whitespace* is ordinary technical writing.

One consequence worth stating: because code spans are replaced before matching, the matched text is no longer the
author's text. These rules report a line number and a description of what was recognised, and never quote an
excerpt back as though it were what the author wrote.

SF4002 turns on a distinction English makes with a single word. "Do not tell the user **that** this ran" conceals
something; "do not tell the user **to** run it twice" is advice about what to say. The pattern refuses the second
with a trailing negative lookahead, and without that it fired on ordinary skill instructions.

Measured on the same 229 real skills the SF3xxx rules were measured against: **2 findings, both SF4001, both
real.** `smart-explore` says "This skill overrides your default exploration behavior"; `using-superpowers` says
"Superpowers skills override default system prompt behavior". Neither is malicious — the second even subordinates
itself to the user's instructions in the next clause — and neither is a false positive either: both are skills
claiming authority over their surroundings, which is exactly what a person deciding whether to install one would
want to see. That is what a signal is.

| Code | On 229 real skills |
|---|---|
| SF4001 | 2 — `smart-explore`, `using-superpowers`, both the "override" pattern |
| SF4002 | 0 |

SF4002 firing zero times is the intended result and proves only one of the two things worth knowing: no false
positives on benign input. Its value rests on the crafted positives in the tests, for the same reason the SF3xxx
rules do — a skill telling an agent to hide its actions is what an attacker writes, and attackers are not in a
sample of benign skills.

Two further groups were considered and **not** shipped: prose instructing credential-file access, and prose
instructing exfiltration to an external destination. Both were speculative — no measurement supported either
shape, and D-29 forbids publishing a rule on a guess about how often it fires. They are candidates for `SF5xxx`,
where provenance and supply chain give them a better home than injection does.

## Supply chain and provenance

| Code | Rule | Status |
|---|---|---|
| SF5001 | The skill fetches something remote from a reference that can change | **Implemented** |

The supply-chain question a skill can honestly be asked from its own text is narrow: *run this tomorrow, get the
same thing?* A URL pointing at a branch, a package resolved to `latest`, a container image with no version, a
latest-release download — all answer no. None of those is a vulnerability. What they have in common is that they
turn somebody else's compromise into yours, silently, without the skill changing.

**This rule reads code blocks on purpose, which is the opposite of SF4001, and the reason is worth stating.** To an
injection rule a fenced block is an example being displayed, so reading it invents findings. To a supply-chain rule
it is the install command the agent will actually run, so skipping it hides them. Same construct, opposite
treatment, because the questions differ.

Measured on the same 229 real skills, and the measurement changed the rule. The first version matched the version
selector alone (`@latest`, `:latest`) and produced four findings, of which **one was a skill giving exactly this
advice** — "Use specific version tags (node:22-alpine, not node:latest)" — with the rule firing on the
counter-example it cited. The same failure that killed SF3002's body scan.

Note what did *not* fix it: markdown structure. That false positive sits inside a fenced block and one of the true
positives sits in an inline code span in a bullet list, so neither "code only" nor "prose only" separates them. The
distinction is grammatical — a fetch has a verb. Requiring one (`npm install`, `npx`, `pip install`, `docker run`,
`FROM`, and their kin) keeps both real install commands and drops the advice.

| | On 229 real skills |
|---|---|
| Selector alone | 4 findings, 1 false positive |
| Selector plus a fetch verb | **3 findings, 0 false positives** |

The cost is stated rather than hidden: a mutable reference invoked through a verb the list does not know is missed.
For a rule nobody asked for, silence is the right direction to fail in, and the list is cheap to extend once a
measurement justifies it.

**Provenance was considered and deferred.** "No source or repository is declared, so the skill's origin cannot be
checked" is a real supply-chain observation, and it would also fire on approximately every skill in existence —
the same class as SF1009 and SF1010, which between them already make `--strict` unusable by default. A third rule
of that shape would make the default report worse without telling anyone anything they could act on. It waits for
a reason to exist beyond being true.

## Version and evolution

| Code | Rule | Status |
|---|---|---|
| SF6001 | The reach grew while the declared version stayed the same | **Implemented** (`diff`) |

The only evolution risk that can be computed honestly, and it needs **two** revisions rather than one — which is
why it belongs to `diff` and not to `validate`. A consumer pinned to `1.0.0` who now receives a skill that can run
shell commands was not protected by their pin, and nothing in the version told them.

It requires a version on both sides. An unversioned skill on both sides makes no promise, so it breaks none; a
version appearing for the first time made no promise about the revision before it.

**"No version is declared" is deliberately not a rule.** Measured: 210 of 229 real skills — 91% — declare no
version. It is a true observation and a useless warning, the same shape as SF1009 and SF1010, which between them
already make `--strict` unusable by default. It was also the reason to reject provenance as an SF5xxx rule, so
letting it in here through the back door would be inconsistent as well as noisy. That is what the both-sides
requirement is protecting.

## Provider compatibility

| Code | Rule | Status |
|---|---|---|
| SF7001 | Compatibility is declared with a provider SkillForge does not recognise | **Implemented** |
| SF7002 | The `name` is longer than a declared provider accepts | **Implemented** |
| SF7003 | The `description` is longer than a declared provider accepts | **Implemented** |

These are reported by `validate` but they are not rules: a rule sees the skill and nothing else, and these also
depend on what the run asked for (`--provider`). They come from `ProviderCompatibilityChecker` and are merged into
the report the same way the loader's diagnostics are, so suppression, ordering, JSON and SARIF apply unchanged.
SF6001 already set the precedent that a code's owner need not be the rule pipeline.

**Nothing is checked against a provider the skill does not name.** A skill is only measured against `claude-code`
if it says `compatibility: [claude-code]`, or if the caller asked with `--provider claude-code`. Judging every skill
against every provider SkillForge knows would report portability problems to authors who never claimed to be
portable — which is the exact failure mode the SF3xxx measurements were introduced to prevent.

### What each provider profile declares

| Provider | `name` limit | `description` limit |
|---|---|---|
| `claude-code` | 64 | 1024 |
| `codex` | not known | not known |
| `cursor` | not known | not known |
| `github-copilot` | not known | not known |

A limit SkillForge has not read from that provider's own documentation is left **unset**, and an unset limit is
never checked — it does not mean "no limit", and it is not filled in with a guess. Those providers are still in the
registry, because recognising the identifier is what stops a legitimate `compatibility: [codex]` being reported as
a typo. Only `claude-code` can produce SF7002 or SF7003 today, and a test asserts that so the moment another
provider's limit is added, the suite says this table has to be updated with it.

The length comparison ignores trailing whitespace, because a block scalar's closing newline is YAML syntax rather
than part of the description. Measured on the finding below: 1065 characters raw, 1064 of description.

### Measured on 229 real skills

| Run | SF7xxx findings |
|---|---|
| Plain `validate` | **0** |
| `validate --provider claude-code` | **1** |

The zero is real but it proves nothing on its own: none of the 229 skills declares `compatibility` at all — SF1010
fires on all 229 — so the checks never ran. That is worth stating rather than presenting as a clean result.

Asking the question explicitly is what produced signal. `--provider claude-code` on the same 229 skills gives one
finding: `vgen-pr`'s description is 1064 characters against a documented limit of 1024. It was verified by parsing
the frontmatter independently, and it is a skill actually installed in Claude Code, so it is a true positive rather
than a crafted one.

SF7001's value cannot be measured on this corpus for the same reason, and is shown with the near-misses its
suggester resolves — `claude_code`, `ClaudeCode`, `claude-cod`, `copilot` — each pinned by a test. When two known
identifiers are equally close it suggests neither, because naming one of two would be a coin toss presented as
advice.

### Why there is no "capability not supported" rule

A fourth code was designed and dropped: "the skill ships scripts, or uses `allowed-tools`, and a declared provider
does not support that". It would need a documented fact per provider about what they execute, and SkillForge has
read none. Shipping the rule with no data behind it would mean shipping a rule that can never fire — or worse,
filling the gap with a guess and reporting a constraint that may not exist. The profile type has room for it; the
code will be added when a measurement justifies one.

## MCP servers

| Code | Rule | Status |
|---|---|---|
| SF8001 | An MCP server is declared over the HTTP+SSE transport, deprecated since `2025-03-26` | **Implemented** (`migrate inspect`) |
| SF8002 | An MCP server is declared at a plaintext `http://` URL on a remote host | **Implemented** (`migrate inspect`) |
| SF8003 | An MCP server's command resolves a package at launch without a pinned version | **Implemented** (`migrate inspect`) |
| SF8004 | A probed server does not implement `server/discover`, so it is a handshake-based revision | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8005 | A probed server declares a capability the specification has deprecated | **Implemented** (`migrate inspect --probe-mcp`) |

**The whole band is `Info`, and that is a decision.** `migrate inspect` describes and does not judge (ADR-006) and always
exits `0`, so nothing here is a gate. It also solves a real measurement problem: SF8003 fires on **three of the four** MCP
servers declared on the machine this was written against. As a warning that is the SF1009 shape — true and nagging. As an
observation in an inventory it is neither.

`validate` never emits SF8xxx: it looks at a skill, and none of this is in a skill.

### Measured on the real declarations

| Code | On 4 real MCP servers |
|---|---|
| SF8003 | **3** — `npx -y @azure-devops/mcp`, and `npx -y obsidian-mcp` twice |
| SF8001, SF8002 | **0** — no HTTP server is declared on this machine at all |
| SF8004, SF8005 | **0** — nothing was probed, because probing is opt-in and there is nothing HTTP to probe |

Those zeros prove nothing on their own, and the same honesty applies as with SF7xxx: the checks did not run rather than
ran clean. Each was verified against a fixture instead — a `/sse` endpoint, a plaintext remote URL, a stub server that
answers `server/discover` and one that returns `-32601`.

Two exclusions came out of that fixture work, and both are load-bearing:

- **Loopback is never reported** by SF8002. `http://127.0.0.1:8801/mcp` exposes nothing to a network, and reporting it
  would train people to ignore the code.
- **A local executable is never reported** by SF8003. Codex declares its own server as an absolute path to an `.exe`; a
  file on disk does not change underneath you, which is the opposite of the property the check is about. Nor is a pinned
  package reported, including a scoped one — `@scope/pkg@1.4.2` pins, `@scope/pkg` does not, and the leading `@` is not a
  version separator.

### What a probe can and cannot say

Only servers reached over **HTTP** are probed, and only with `--probe-mcp`. A stdio server is never launched: inspecting
a local server by running it is the exact act SkillForge exists to let somebody defer until they have looked. Such a
server is reported as "not asked", with the reason, so it cannot be mistaken for one that failed to answer.

A probe is one `server/discover` request, which `2026-07-28` made mandatory for servers and which returns supported
versions, capabilities and identity together. The reported identity is always labelled **self-reported**: the
specification states plainly that `serverInfo` is not verified by the protocol and that clients should not use it for
security decisions, so printing it as bare fact would repeat a claim as though SkillForge had checked it.

SF8005 looks for **`logging` only**. Roots and Sampling were deprecated by the same SEP and are listed beside it
everywhere, but they are *client* capabilities — a server cannot declare them, so looking for them here would be a check
that can never fire. That correction came from the specification's own deprecated-features registry, against a secondhand
summary that grouped all three as server-side.

## Measured against real skills

Run over 32 skills installed on a working machine (2026-07-27), the rules behaved like this:

| Code | Skills affected |
|---|---|
| SF1010 — no compatibility declared | 32 of 32 |
| SF1009 — no license declared | 30 of 32 |
| SF1002 — description states no activation context | 3 |
| SF1003 — `SKILL.md` over 500 lines | 1 |

No errors, and nothing crashed — the loader and the error rules hold up on real input.

A second, larger run — 229 skills in one batch, including a collection where skills deliberately link to each
other — added one finding the smaller run could not show: **SF0008 fired 21 times on cross-skill references**
like `../react-testing/SKILL.md`. Those are not mistakes. A collection of skills that reference their siblings
is a real and reasonable pattern, and calling it an error fails the build over it.

The rule was still telling the truth — such a reference cannot survive being packaged on its own — but "cannot
be packaged alone" and "the author made a mistake" are different claims, and only the second deserves an error.

**This has since been fixed, and not the way it was first sketched.** The plan was to pass a collection root
into the rules so they could tell "outside the skill" from "outside the collection". That turned out to be
unnecessary: the distinction is provable from the reference text alone. One level up and back down into a named
directory *is* a sibling, by construction; two or more levels up, an absolute path, or the parent directory
itself cannot be. So no collection root, no context object threaded through every rule, and the answer is the
same whether one skill or a whole directory is being validated.

The single rule became two, keeping one code per rule: **SF1011** (warning) for a sibling reference and
**SF0008** (error) for anything reaching further. Measured again on the same 229 skills: **21 errors became 6
errors and 15 warnings**, and skills with errors went from 6 to 5. The six that remain all reach out of the
skills tree entirely — `../../ECC-Tools`, `../../rules/react/`, `../../docs/...` — which is what SF0008 is for.

The two warnings at the top are worth reading carefully. They are not finding mistakes; they are finding that
the `SKILL.md` convention in the wild does not carry `license` or `compatibility` at all. A warning that fires
on virtually every input is noise, and noise teaches people to ignore warnings. The practical consequence is
that **`--strict` fails all 32**, so it cannot be recommended as a default gate for existing skills — only for
a repository that has decided to adopt these two fields.

SF1009 and SF1010 are **not** changed in response to this measurement. Their severity is part of the published
contract, and the answer for a repository that does not want them is configuration, not a quiet downgrade.

That configuration now exists:

```bash
skillforge validate ./skills --suppress SF1009,SF1010 --strict
```

```yaml
# skillforge.yaml, per skill
validation:
  strict: false
  suppress:
    - SF1009
    - SF1010
```

Suppression is deliberately unrestricted — errors can be suppressed too, because a repository that has decided a
rule does not apply to it has a reason SkillForge cannot see. What keeps that honest is that **the count is
always reported**: `Suppressed: 2` in the console, `summary.suppressed` in JSON. A report that quietly omitted
findings would be lying about what was checked.

The difference between the two responses is worth stating: SF0008 was **wrong** about a legitimate pattern, so
it was fixed. SF1009 and SF1010 are **right** but unwanted by most existing skills, which is a configuration
problem, not a correctness one.

### The three permission rules, measured on 229 skills

| Code | Findings | Reading |
|---|---|---|
| SF1006 | 11 skills | About five percent ship a script without saying so. Proportionate. |
| SF1007 | 5 findings | All of them `rm -rf`. |
| SF1005 | 0 | The right kind of zero: no skill in the sample ships a `skillforge.yaml`, so none has made a claim to contradict. |

SF1007 firing on only one of its seven patterns proves nothing by itself — a regex that matches nothing and a regex
that is broken look identical from the outside. Each pattern therefore has a known-positive and known-negative test,
which is how the scarcity is known to be real rather than a bug.

## What is still planned, and why it is not here yet

SF1004 to SF1008 are reserved but unimplemented, and that is a scope decision rather than an oversight.
Each needs something this release deliberately does not do:

| Code | What it would need |
|---|---|
| SF1004 (unused file) | A definition of "used" beyond a Markdown link. A script invoked by another script, or a file an agent is expected to discover by convention, would be reported as unused today — a false positive that trains people to ignore warnings. |
| SF1008 (unpinned dependencies) | A definition of what a skill's dependencies are. Nothing in the format declares them yet. |

## Security signals

Milestone v0.2.0 detects the patterns listed in the roadmap: piped shell installers, `rm -rf`,
`Invoke-Expression`, `chmod 777`, `sudo`, privileged containers; sensitive paths such as `.env`, `.ssh`
and `/etc/`; network calls; secret-shaped identifiers.

Today `inspect` reports the neutral facts underneath those signals — that a skill ships a script, points at
a URL, or contains a binary — without interpreting them.

These checks only ever produce diagnostics. SkillForge does not classify a skill as safe or malicious
(ADR-006).
