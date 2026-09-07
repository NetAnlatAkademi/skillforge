# Validation rules

Every rule owns a stable diagnostic code. Codes are never reused or renumbered once released.

- `SF0xxx` — **Error**. The skill is not usable as written.
- `SF1xxx` — **Warning**. The skill works but quality or risk deserves attention.
- `SF2xxx` — **Info**. A neutral observation about the skill's surface.

Six further bands cover the work after v0.1.0: `SF3xxx` activation and retrieval risks, `SF4xxx` instruction
injection, `SF5xxx` supply chain and provenance, `SF6xxx` version and evolution, `SF7xxx` provider compatibility,
`SF8xxx` MCP servers, protocol and policy, `SF9xxx` organisation policy. Every one of them has rules.

A band is a **scope**, not a command: `SF6xxx` is reported by `diff`, `SF8xxx` by `migrate inspect`, `SF9xxx` by
`policy check`, and "a file I could not read" stays `SF1xxx` wherever it happens.

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
| SF1003 | 1000 lines | A long entry point means reference material should live in its own file that the agent reads only when needed. Raised from 500 on 2026-07-29 — see the measurement below. |
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
| SF1003 | `SKILL.md` is longer than 1000 lines | **Implemented** |
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
| SF1016 | A remote registry could not be searched, or answered with something unreadable | **Implemented** (`discover`) |

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

## Distribution and update drift

| Code | Severity | Rule | Reported by |
|---|---|---|---|
| SF5101 | Warning | Nothing observed says where an asset came from | `provenance diff` |
| SF5102 | Info | An asset declares an upstream it is not distributed from | `provenance diff` |
| SF5103 | Warning | An asset's files differ from the revision it names | `provenance diff` |
| SF5201 | Warning | The marketplace an asset is distributed through changed | `provenance diff` |
| SF5301 | Warning | An asset that was not updating itself now does | `provenance diff`, `update analyze` |
| SF5302 | Warning | An asset that named an immutable revision now names one that can move | `provenance diff` |
| SF5303 | Error | An update that arrives without review added a capability | `update analyze` |
| SF5401 | Error | The publisher changed between two revisions of the same asset | `provenance diff`, `update analyze` |
| SF5501 | Error | The content fingerprint changed while the declared version did not | `provenance diff` |

`SF5001` is a rule about a skill's own references and predates this block. `SF5101` onwards are about
**distribution**: where an asset came from, who publishes it, and how it gets its next version. Both are `SF5xxx`;
the numeric gap is what lets a reader tell the two apart in one report.

### Why these are diff codes, not scan codes

The deferral recorded above still stands: *"no source is declared"* fires on approximately every skill in
existence, and a third rule of that shape would make the default report worse. `provenance` therefore reports and
never judges — it exits `0` over a tree where nothing can be traced anywhere.

What changed is that **drift** is a different question from **absence**. `company` becoming `unknown-publisher` is
not a fact about the state of the world; it is an edit somebody made, in a pull request somebody is reviewing, and
it fires exactly once. Every code in this block is about a transition, which is why they live in `provenance diff`
and `update analyze` rather than in `validate` or `scan`.

The asymmetry is deliberate everywhere it appears. An asset that *gained* a publisher, a marketplace or a pin is
shown in the report and coded nowhere — a reviewer who tightened a distribution does not need a warning about it,
and a command that warned about every edit teaches people to skip its output.

### SF5303 is the code the auto-updating plugin made necessary

Package hashes answer "is this the file I approved". They cannot answer "will the file I approved still be the one
running tomorrow", and an enterprise marketplace with auto-update turned on means the answer is often no.

`update analyze` reads both halves and only reports the combination:

| Update mode | What the update adds | Risk |
|---|---|---|
| Pinned | nothing that runs, reaches or reads | Informational |
| Pinned | a script, a host, an MCP server, a credential source | Medium |
| Automatic or floating | a script, a host, an MCP server, a credential source | **High**, plus `SF5303` |
| Any | a different publisher | **High** |
| Automatic or floating | a different publisher | **Critical** |

A new reference file is a change and not an expansion. So is a removal: an update that gives something up is shown
and never coded, for the same reason a tightened policy is not a `policy diff` finding.

### What a credential source is, exactly

The name of an environment variable an MCP declaration reads, where the name contains one of `token`, `key`,
`secret`, `password`, `credential`, `auth`, `session` or `cookie`. **The value is never read**: the configuration
readers take the property names out of the `env` and `headers` objects and drop the values on the floor, so there
is no filter on the way out to get wrong.

That makes it a name-shaped heuristic, and it is labelled as one in the output. `DEPLOY_TOKEN` makes a declaration
look like it carries a token; nothing here proves that it does.

### Update mode, and why "unknown" is the default

| What the marketplace entry says | Mode |
|---|---|
| `autoUpdate: true`, or `updateMode: automatic` | Automatic |
| A commit SHA, or a version tag (`v1.4.2`, `2.0.0-beta.1`) | Pinned |
| A branch, `latest`, `main`, `HEAD`, or a repository with no revision | Floating |
| Nothing | **Unknown** |

An entry that says nothing is `Unknown` rather than `Pinned`. Reporting the safest possible mode for the least
evidence is how a supply-chain report ends up reassuring somebody about a plugin nobody has looked at.

A version tag counts as pinned even though a tag can be moved. Republishing a tag is a supply-chain event in
itself, and reporting every tagged install as floating would bury the entries that genuinely are.

## Version and evolution

| Code | Rule | Status |
|---|---|---|
| SF6001 | The reach grew while the declared version stayed the same | **Implemented** (`diff`) |
| SF6002 | A permission the earlier revision did not declare | **Implemented** (`diff`) |
| SF6003 | A script the earlier revision did not ship | **Implemented** (`diff`) |
| SF6004 | A host the earlier revision did not point at | **Implemented** (`diff`) |
| SF6005 | Something the earlier revision reached is gone | **Implemented** (`diff`) |

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

### SF6002 to SF6005 exist so a diff can be uploaded, not so it can be listed

`diff --format sarif` needed an answer to "which part of a diff is a finding". Most of a diff is not: a changed
description, a new reference file, a compatibility declaration. Those are shown in the console and JSON reports and
stop there, because putting them in SARIF would use a findings format to carry a summary.

What is left is the three ways a skill's reach grows — SF6002, SF6003, SF6004 — each one anchored on a file so the
annotation lands somewhere, each naming the thing that was added. SF6005 is their opposite and is deliberately
**Info**: a skill that reaches less far than it did is not a risk, but a consumer relying on what was removed still
needs to see it. Everything given up becomes one SF6005 rather than one each, so a revision that dropped six
permissions does not bury the one it added.

`diff` still exits on the same rule it always did: a new **validation error** fails it, and a surface change alone
does not unless `--fail-on-change` says so. The SARIF results are what a reviewer reads; they are not a verdict.

Whether a changed description broadened the activation scope is still not claimed, in any format. A shorter
description can match more, a longer one can match more, and the words that matter depend on the agent. `diff`
reports that it changed and shows both.

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

### SF1003's threshold moved from 500 to 1000

Measured on the 229-skill corpus, before and after: **33 findings at 500, 0 at 1000.** The longest `SKILL.md` in the
corpus is 734 lines.

Inspecting the longest of those 33 showed instructions that are long because the job is long, not because reference
material sat in the wrong file — and a warning that speaks about a seventh of real input is the SF1009 shape: true,
unactionable, and teaching people to skim past warnings.

The honest cost is stated rather than buried: at 1000 the rule now fires on **nothing** in the corpus, so its value rests
entirely on entry points that are genuinely unusual rather than merely long. A test pins 734 as passing, so any future
tightening has to face the fact that it would start speaking about a real skill again.

## Agent identity

| Code | Severity | Rule | Reported by |
|---|---|---|---|
| SF7101 | Warning | The kind of identity an MCP server is reached with changed | `identity diff` |
| SF7102 | Warning | A credential that does not expire replaced one that did | `identity diff` |
| SF7201 | Warning | An identity asks for scopes it did not ask for before | `identity diff` |
| SF7202 | Warning | An identity that acted for itself now acts on behalf of another party | `identity diff` |

`SF7001`–`SF7003` are about agent providers and predate this block. `SF7101` onwards are about the identity an
agent connects with, and `SF7301` onwards about the tool surface a server opens to it. Same band, three blocks; the
gaps are what let a reader tell them apart.

### The question these answer

Not "is this server authorised" — **whose authority is the agent using**. A person's OAuth session is bounded by
what that person may do and disappears when they leave. A workload identity is bounded by what the workload was
granted, which is usually more, and it does not. Between two revisions of a configuration, that substitution is one
line in a diff.

### Inference from names, and nothing else

**No credential value is ever read, stored or printed.** Every signal is the *name* of an environment variable or
of a header, or something a server's own `401` stated. That is a real limit, and it is printed in the output rather
than papered over.

| Evidence | Identity |
|---|---|
| `STS_SUBJECT_TOKEN`, `ACTOR_TOKEN`, `*_TOKEN_EXCHANGE` | Token exchange, always delegated |
| `AWS_WEB_IDENTITY_TOKEN_FILE`, `AZURE_FEDERATED_TOKEN_FILE`, `IDENTITY_ENDPOINT` | Workload identity |
| `*SERVICE_ACCOUNT*`, `GOOGLE_APPLICATION_CREDENTIALS`, `*CLIENT_SECRET` | Service account, long-lived |
| `*OAUTH*`, `*REFRESH_TOKEN`, `*ACCESS_TOKEN` | User OAuth |
| `*API_KEY*`, `*_KEY`, `*TOKEN*`, `*SECRET*` | API key, long-lived |
| A `Bearer` challenge and nothing else | User OAuth — the weakest signal, checked last |
| Nothing | **Unknown** |

Where several apply, the most specific machine identity wins. A declaration carrying both a federated token file
and an API key is reported as a workload identity, because that is the one that decides what the agent can actually
do; naming the API key would report the credential a reviewer least needs to hear about.

`subject` is always `null`. A subject lives inside a token, and SkillForge does not read tokens. The field exists
because a diff of subjects is what identity drift looks like, and it has to exist before anything can fill it
honestly.

Scopes and issuers come only from a probed server's `401`, so without `--probe` they are empty rather than assumed.

## MCP tool surface

| Code | Severity | Rule | Reported by |
|---|---|---|---|
| SF7301 | Warning | A server exposes at least as many tools as the configured threshold | `mcp surface --probe` |
| SF7302 | Warning | A large surface includes tools that change, govern or reach a secret | `mcp surface --probe` |
| SF7401 | Info | Nothing observed says the server narrows what it exposes as a task goes on | `mcp surface --probe` |

Every tool a server exposes is in the model's context whether or not the task needs it, so a server with 147 tools
is a decision about every conversation the agent has — and it is usually a decision nobody made, because the number
arrives one tool at a time.

### The thresholds are configurable, and the defaults say so

```yaml
# .skillforge/policy.yaml
rules:
  mcp:
    surface:
      warningToolCount: 50
      highToolCount: 100
```

Without a policy the defaults above apply and the report labels them as defaults. They are a starting point, not a
measurement: the right number depends on the model's context window, on how the tools are named and on what the
team is doing. What is knowable from here is that the number matters and that somebody should pick it deliberately.

### No model is used, and the categories are name-shaped

Write, credential and admin are read from tool **names** against lists that live in `McpSurfaceAnalyzer` where they
can be argued with. Asking a model whether a tool is dangerous would make the same configuration produce different
reports on different days, which is not a check anybody can put in a pipeline.

`SF7302` fires only when the surface is *also* at or above the threshold. A server with two privileged tools is
most of what MCP is for; a server with sixty tools, nine of which delete things, is the case worth a sentence.

### Zero is not the same as unknown

A tool count comes from the server's own `tools/list`, so it exists only for a server that was probed. A server that
was not probed carries the reason instead of a count, and a stdio server says why it will never have one:
SkillForge does not launch a local server to inspect it. Printing `0` there would tell a reader the surface is tiny
when nobody looked at it.

`SF7401` is deliberately `Info` and deliberately gated on the same threshold. Progressive discovery is new, most
servers do not implement it, and a mechanism nothing recognises is reported as *not detected* rather than as *not
implemented*.

## MCP servers

| Code | Rule | Status |
|---|---|---|
| SF8001 | An MCP server is declared over the HTTP+SSE transport, deprecated since `2025-03-26` | **Implemented** (`migrate inspect`) |
| SF8002 | An MCP server is declared at a plaintext `http://` URL on a remote host | **Implemented** (`migrate inspect`) |
| SF8003 | An MCP server's command resolves a package at launch without a pinned version | **Implemented** (`migrate inspect`) |
| SF8004 | A probed server does not implement `server/discover`, so it is a handshake-based revision | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8005 | A probed server declares a capability the specification has deprecated | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8006 | A server requires authorization but its challenge names no Protected Resource Metadata | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8007 | A tool's `inputSchema` is absent or is not a JSON object | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8008 | A tool's `x-mcp-header` annotation breaks a constraint a client must reject the tool over | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8009 | A tool name falls outside the specification's naming guidance | **Implemented** (`migrate inspect --probe-mcp`) |
| SF8010 | A server's `tools/list` was not read to the end, so its tool count is a floor | **Implemented** (`mcp surface --probe`) |

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
| SF8004–SF8009 | **0** — nothing was probed, because probing is opt-in and there is nothing HTTP to probe |

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

### Authorization, and the rule that is only about the gap

A probe reports how to authorise against a server, because a `401` answers that without credentials: the scheme, the
`resource_metadata` URL a client must follow to find the authorization server, and the `scope` when the server names one.
That is reported in the probe section, **not** as a finding — a server challenging correctly is behaving correctly.

**SF8006 fires only on the gap**: a server that requires authorization and names no `resource_metadata`. MCP servers
**MUST** implement OAuth 2.0 Protected Resource Metadata (RFC 9728) and clients **MUST** use it for discovery, so a
challenge without it leaves a conforming client with nowhere to look. One code, one meaning.

SkillForge does not fetch the metadata document, nor the authorization server's own metadata. Those are further requests
to further hosts, reporting configuration that belongs to neither the skill nor the MCP server, and "one request per
server, plus one for tools" is a property worth keeping.

### Tool conformance — and the rule that was deliberately not written

**There is no "must be JSON Schema 2020-12" rule.** A secondhand summary said `2026-07-28` requires it. The specification
says `inputSchema` "defaults to 2020-12 if no `$schema` field is present" and then shows an explicit **`draft-07`** schema
as a valid example. That rule would have failed conforming servers. The declared dialect is reported and never judged —
there is a test named for it.

What the specification does state at MUST level, and what is therefore checked:

| Code | The requirement it comes from |
|---|---|
| SF8007 | `inputSchema` **MUST** be a valid JSON Schema object, not `null`. Checked structurally — a schema that is present and is an object is taken at its word, because a full validator is a different tool. |
| SF8008 | An `x-mcp-header` value **MUST** be non-empty, a valid HTTP field name, free of CR/LF, case-insensitively unique within the schema, and applied only to `integer`, `string` or `boolean` — `number` is named in the specification as **not** permitted. |
| SF8009 | Tool names **SHOULD** be 1–128 characters, use only letters, digits, `_`, `-` and `.`, and be unique within a server. SHOULD-level, so it is the mildest observation of the set. |

SF8008 is the one worth explaining. The obligation lands on the **client**: a Streamable HTTP client **MUST** reject a
tool definition that breaks these constraints, excluding that tool from `tools/list`. A server shipping one has a tool
that will simply not appear, with nothing in its own logs to say why. Nothing SkillForge does is affected by it, which is
exactly why somebody should be told.

`x-mcp-header` annotations are read from the schema's **top-level properties only**. The specification restricts the
annotation to properties *statically reachable* from the schema root, and deciding reachability through `$ref`s and
composition keywords is a schema resolver's job. Reading nested properties without that resolution would report
annotations that are not actually reachable — a false positive about a constraint whose whole consequence is a client
silently dropping a tool.

### What a probe can and cannot say

Only servers reached over **HTTP** are probed, and only with `--probe-mcp`. A stdio server is never launched: inspecting
a local server by running it is the exact act SkillForge exists to let somebody defer until they have looked. Such a
server is reported as "not asked", with the reason, so it cannot be mistaken for one that failed to answer.

A probe is one `server/discover` request, which `2026-07-28` made mandatory for servers and which returns supported
versions, capabilities and identity together. A server that declares the `tools` capability is then asked for its tool
list — see [The whole tool list, and its three bounds](#the-whole-tool-list-and-its-three-bounds). The reported identity is always labelled **self-reported**: the
specification states plainly that `serverInfo` is not verified by the protocol and that clients should not use it for
security decisions, so printing it as bare fact would repeat a claim as though SkillForge had checked it.

### The whole tool list, and its three bounds

`tools/list` is paginated, and SkillForge follows `nextCursor` to the end. It used to read the first page only, which
made every tool count a first-page count wearing a total's clothes: "this server exposes 50 tools" and "the first page
of this server's tools holds 50" are different sentences, and a declared-versus-runtime comparison built on the second
reports every tool past page one as missing.

Following a cursor the server controls is an unbounded walk by construction, so there are three bounds:

- **A hundred pages.** At any realistic page size that is thousands of tools, far past the point where the surface
  report has already said everything there is to say about a server this large. Not configurable: a limit nobody has
  needed to change is not a setting.
- **A repeated cursor stops the walk.** A cursor is opaque, so there is no other way to tell that following it makes
  progress. A server handing back one it has already given out would loop forever.
- **The cancellation token is checked before every request**, not only on entry. A hundred round trips to a slow server
  is exactly the wait somebody presses Ctrl+C during.

**SF8010 is emitted whenever a bound fires**, at any tool count and before any threshold is applied, because it is a
statement about the number rather than about the server: a count that stopped early cannot be compared against a
threshold, and a reader who is not told will compare it anyway. `Info`, not a warning — SkillForge stopping is a fact
about the report, not a defect in the server. The one case that *is* a server defect, a repeated cursor, says so in the
suggestion.

**A tool named twice is one tool.** The specification identifies tools by name, and a server listing one name on two
pages has made its own surface ambiguous — a client cannot know which schema applies. The first description wins, which
is arbitrary and stated; taking the last would be arbitrary and unstated. Names are compared **case-sensitively**: the
specification does not say `Deploy` and `deploy` are the same tool, and folding case would silently merge two real ones.

**The merged list is ordered by name**, not by the order the server listed them. A paging server may return its pages in
whatever order it likes, and a report that changes between two identical runs cannot be diffed in a pipeline. What the
first response carried survives as a count — `initiallyExposed` in `mcp surface`, which is the number actually in the
agent's context after one round trip — rather than as a position in the list.

SF8005 looks for **`logging` only**. Roots and Sampling were deprecated by the same SEP and are listed beside it
everywhere, but they are *client* capabilities — a server cannot declare them, so looking for them here would be a check
that can never fire. That correction came from the specification's own deprecated-features registry, against a secondhand
summary that grouped all three as server-side.

## MCP policy

| Code | Severity | Rule | Reported by |
|---|---|---|---|
| SF8101 | Error | An MCP server the policy does not permit is declared | `policy check --mcp` |
| SF8102 | Warning | An allow rule now covers everything an earlier one did, and more | `policy diff` |
| SF8103 | Error | An entry in the policy's `mcp` section could not be interpreted | `policy check`, `policy diff` |
| SF8104 | Error | The policy governs MCP servers without denying by default | `policy check`, `policy diff` |
| SF8105 | Warning | A server is permitted by its display name alone | `policy check --mcp`, `policy diff` |
| SF8106 | Warning | A local MCP command is permitted that was not before | `policy diff` |
| SF8107 | Warning | A remote MCP endpoint is permitted that was not before | `policy diff` |

These extend the `SF8xxx` band into a second block. `SF8001`–`SF8009` are what a **declaration says** — informational
by design, reported by `mcp inspect` and `migrate inspect`. `SF8101` onwards are what an **organisation decided**, so
they carry the severities policy findings carry everywhere else in the tool. Both are MCP, which is why they share a
band; the numeric gap is what lets a reader tell a fact from a verdict in one report.

### The policy this reads

```yaml
schemaVersion: 1

rules:
  mcp:
    default: deny

    allow:
      - serverUrl: "https://mcp.company.com/*"
      - serverCommand:
          command: npx
          args:
            - "@company/internal-mcp"
      - serverName: "internal-notes"

    deny:
      - serverUrl: "http://*"
```

`serverCommand` also accepts the short form `serverCommand: npx`, which matches on the executable alone. It is the
executable **only**: `serverCommand: "npx @vendor/mcp"` matches a declaration whose command is that whole string,
which is not how a configuration file writes one. Use the mapping form to constrain arguments. The `rules:` wrapper
is optional, as it is everywhere else in the file.

### How a rule is matched

**Deny is checked before allow.** A rule written to block something cannot be undone by a broader allow beside it.

**A rule only matches the transport it is about.** A `serverUrl` rule never matches a local command and a
`serverCommand` rule never matches a URL, so a policy cannot accidentally permit a process launch by naming a web
address. A `serverCommand` rule with `args` requires each of them to appear among the declared arguments — by
containment rather than by position, because a `-y` in front of a package name is noise, not an identity.

**URLs are canonicalised before comparison**: scheme and host lower-cased, a default port dropped, a trailing slash
dropped. `HTTPS://MCP.Company.com:443/github/` and `https://mcp.company.com/github` are one endpoint, and a policy
that blocked one while permitting the other would be evaded by whoever wrote the configuration.

**`*` is the only wildcard**, and it matches any run of characters. Everything else in a pattern is literal — a `.`
in a hostname is a dot, not "any character".

**Matching ignores case throughout**, including URL paths, which the standard treats as case-sensitive. That is
deliberate and it cuts one way: an allow rule matches slightly more than it strictly should, and a **deny** rule
matches everything it should. A deny that misses is worse than an allow that is generous.

### The default, and what happens when it is missing

`SF8104` fires when the section governs servers and does not say `default: deny` — whether it says `allow` or says
nothing at all. It is an error either way, but the two are not treated the same:

- `default: allow` — a server no rule names is permitted, as the policy says.
- **no default at all** — SkillForge applies **no default**. It checks the explicit rules, blocks nothing else, and
  reports `SF8104`. Guessing "deny" here would be fail-closed in the letter and useless in practice: a policy with a
  deny list and no allow list would produce one `SF8101` for every server on the machine, burying the actual problem
  — that nobody wrote the decision down — under findings the tool invented. The run still fails, because `SF8104` is
  an error. Fail-closed in outcome, not in noise.

An entry that cannot be interpreted is dropped and reported as `SF8103`, never guessed at. Applying a rule nobody can
read would enforce something nobody wrote; dropping it quietly would leave a deny list weaker than its author
believes.

### What `policy check` still cannot see

`allowedProtocolVersions` and `denyDeprecatedCapabilities` describe a **running server**, not a declaration, so they
remain `SF9009`. `mcp inspect --probe-mcp` is what asks a server, and it reports SF8004 and SF8005.

Allow and deny rules with no `--mcp` argument also report `SF9009`: the rules had nothing to be applied to, and a
rule that never ran must not look like a rule that passed.

### Measured before published

On this machine's real MCP configurations — three servers under `~/.claude.json`, one under `~/.codex/config.toml` —
an **empty policy produces zero findings**, and a policy of `default: deny` with no allow list produces one `SF8101`
per server plus one `SF8104`, which is the arithmetic the rule promises. The number that matters is the first: a
policy that has not decided anything about MCP is silent, so the command is safe to put in a pipeline before the
rules exist.

## Discovery drift

The codes that answer the question a registry cannot answer about itself.

| Code | Severity | Rule | Reported by |
|---|---|---|---|
| SF8201 | Warning | A discovered MCP server answered with a tool the registry did not declare | `discovery verify --probe` |
| SF8202 | Info | A registry declares a tool the server did not answer with | `discovery verify --probe` |
| SF8203 | Info | The tool counts differ and the names could not be compared one to one | `discovery verify --probe` |
| SF8204 | Info | The version the registry names and the one the server reports about itself disagree | `discovery verify --probe` |
| SF1016 | Warning | A registry could not be searched, or answered with something unreadable | `discover`, `discovery verify` |

**SF8201 is the finding the feature exists for.** A registry says a server has twelve tools; the server answers with
seventeen. Discovery metadata is written by a publisher, and a tool surface is answered by a process — the five that
appear only at runtime are capabilities nobody reviewed, and that is the shape an unreviewed capability arrives in.

### Measured before the severities were fixed

Eight fixture listings: four matching their servers exactly, two whose servers answer with an extra tool, one whose
listing declares a tool the server dropped, and one declaring no capabilities at all.

| Code | Findings on 8 fixtures |
|---|---|
| SF8201 | **2** — only the two servers with a genuinely unlisted tool |
| SF8202 | **1** |
| SF8203 | **1** |
| On the four matching listings | **0** between them |

That last row is the number that matters. A rule that fired on a correct listing would be a rule nobody could put in
a pipeline — the SF8003 problem, which is why SF8003 is information. Two out of eight is a rate a reader will
actually read, so SF8201 is a Warning. The measurement is a test, not a note: `MeasuredOnFixturesBeforeTheSeverities
WereFixed` in `DiscoveryVerifierTests`, so the numbers above fail the build if they stop being true.

### Silence is not a claim

A registry that declares no capabilities has **not** declared that the server has none. It has said nothing.

Comparing runtime names against nothing would report every tool on every such server as unexpected, which would make
SF8201 fire on every listing that omits an optional field — and the MCP Registry lists no tools at all, so that is
most of them. When there is nothing to compare names against, only the counts are stated, as SF8203.

### An incomplete tool list suppresses only one of the two directions

When `tools/list` stopped early (SF8010), SF8202 is not emitted: a tool absent from a partial read might be on the
page nobody reached, and reporting it would be a finding produced by SkillForge's own page limit rather than by the
server.

SF8201 still fires. The asymmetry is the point — a tool that **was** seen is present whether or not reading
finished, while a tool that was not seen might simply not have been reached.

### Endpoint drift has no code, deliberately

"The registry listed it at one host and another host answered" is a real drift kind, and `DiscoveryDriftKind`
names it. Nothing emits it, and no code has been published for it: establishing it means knowing the URL that
answered after redirects, and the MCP prober reports what a server *said* rather than where the socket ended up. A
drift kind that cannot be established would be an empty promise on a report.

### What `VerifiedNoDrift` does not mean

It means the capabilities a registry declared matched the tools a server answered with, at the moment it was asked.

It does not mean the server is trusted. It does not mean it is safe. A server can match its listing exactly and
still be malicious, still be compromised tomorrow, and still expose a tool that drops a database. Every output path
says so in those words, and `ResourceVerification` has no field that could say otherwise — there is a test that
asserts the absence.

Whether a difference is *permitted* is `policy check`'s answer. The two layers stay apart so that "unexpected
runtime tool: `delete_database`" is a description and "`delete_database` is not permitted" is a violation, rather
than one confused sentence that is neither.

## Organisation policy

| Code | Rule | Status |
|---|---|---|
| SF9001 | The policy file could not be read or parsed, so nothing was checked | **Implemented** (`policy check`) |
| SF9002 | Shell access, where the policy forbids it | **Implemented** (`policy check`) |
| SF9003 | A declared filesystem write, where the policy forbids it | **Implemented** (`policy check`) |
| SF9004 | A host the policy's allow-list does not name | **Implemented** (`policy check`) |
| SF9005 | The origin cannot be identified, where the policy requires it | **Implemented** (`policy check`) |
| SF9006 | No license, where the policy requires one | **Implemented** (`policy check`) |
| SF9007 | `SKILL.md` longer than the policy allows | **Implemented** (`policy check`) |
| SF9008 | A suppression in the policy gives no reason, so it was not applied | **Implemented** (`policy check`) |
| SF9009 | A policy rule was read but could not be checked | **Implemented** (`policy check`) |
| SF9010 | A policy rule outside the `mcp` section was relaxed between two snapshots | **Implemented** (`policy diff`) |

**SF9010 is one code for every non-MCP relaxation**, not one per rule. Each finding names the rule and both values,
so nothing is lost; what a code per rule would add is the ability to suppress them separately, and nobody has asked
for that. The MCP relaxations have their own codes because the review that asked for `policy diff` asked for those by
number.

It is a `Warning` where the rest of the band is `Error`: relaxing a policy is a decision an organisation is entitled
to make, and `policy diff` describes the change rather than refusing it. `--fail-on-weakening` is how a pipeline
turns it into a gate.

**A change is only coded when some command enforces the rule it changes.** `filesystem.write.allowed` path lists,
`requirePackageHash` and `allowedProtocolVersions` are all `SF9009` in `policy check` — unobservable — so a change to
them is shown in the diff and carries no code. Warning about a widened guarantee that nothing checks would be a
warning about nothing.

**`SF9xxx`, not `SF8xxx`.** The work plan put organisation policy in the `SF8xxx` band, which by then already meant MCP
in nine published codes. A published code's meaning never changes, so the band moved rather than the codes.

**Every rule here is opt-in and there is no default that forbids anything.** A policy that says nothing produces
nothing, which is what makes adopting `policy check` safe: it cannot start failing a build over a decision nobody made.
That is also why these rules were not measured against a corpus the way SF1xxx and SF3xxx were — they fire exactly as
often as an organisation asks them to, and a rule nobody has written cannot be noisy.

`Error` rather than `Warning`, for the same reason. A policy is a decision somebody wrote down; a violation of it is not
advice. The escape hatch is a suppression, which must carry a reason:

```yaml
suppress:
  - code: SF9002
    skill: dotnet-api-review
    reason: "approved in TICKET-123"
```

A suppression with no `reason` is **refused and reported as SF9008**, rather than applied or silently dropped. A policy
that can silence a rule without recording why has stopped being a record of decisions; an author whose suppression was
quietly ignored would believe it worked.

### What each rule is judged on

SF9002 is judged on two independent signals — the commands the skill declares under `permissions.shell` in its own
`skillforge.yaml`, and any script it ships. A skill that ships a script has shell reach whether or not it admitted to
it. SF9003 is judged on the **declaration alone**: nothing in a skill's contents proves it writes, and inferring a write
from the presence of a script would fire on every skill that has one.

SF9004 compares **hosts**, matching what `diff` compares. An allow-list is a list of who a skill may talk to, not of
which pages it may link. An `allowedDomains` key that is absent is silence and checks nothing; an *empty* list is a
decision that no host is allowed, and the two do not collapse into each other.

SF9005 requires a repository, a commit, a path within it, **and** a clean working tree. A dirty tree fails it on
purpose: the commit is named, but it is not what would be published.

### SF9009 — the rule that says a rule did not run

Three things in the documented schema cannot be answered by looking at a skill, and each produces an `Info` naming
itself rather than passing quietly:

| Rule | Why it cannot be checked here |
|---|---|
| `permissions.filesystem.write.allowed` as a **list of paths** | A skill declares that it writes, never where. Confining it to `./reports/**` can only be verified by watching it run. |
| `provenance.requirePackageHash` | Every package `pack` produces carries a SHA-256, so the rule cannot fail. It belongs against a package's manifest. |
| the `mcp` section | Protocol versions and deprecated capabilities are properties of a running server. `migrate inspect --probe-mcp` is what asks one, and it reports SF8004 and SF8005. |

A rule that never runs looks exactly like a rule that passed, and the difference is the entire value of having written
the rule down. `allowed: false` **is** checkable and is checked; only the path list is not.

A policy that cannot be parsed fails the run with SF9001 and checks nothing — unlike `skillforge.yaml`, which is
advisory and is ignored with SF1012. A build that goes green because the rules failed to load is the worst outcome
available to this command.

### Measured, with the strictest policy the schema can express

230 real skills, judged against a policy that forbids shell, forbids writes, allows two hosts, requires a commit SHA,
requires a license and caps `SKILL.md` at 500 lines:

| Code | Skills affected |
|---|---|
| SF9005 — origin not identifiable | 229 |
| SF9006 — no license | 214 |
| SF9004 — host not on the allow-list | 101 |
| SF9002 — shell access | 39 |
| SF9007 — `SKILL.md` too long | 34 |

Those numbers would be damning for a validation rule and mean nothing here: the policy was written to fire. SF9005 at
229 is a directory of skills that is not a checkout, which is exactly what the rule is for. The measurement worth having
is the opposite one — **an empty policy over the same 230 skills produces 0 policy findings**. The only thing it
reports is one skill whose frontmatter will not parse, which is SF0003 and would be reported by anything that opened
it. That zero is the property that makes the command safe to add to a pipeline before the rules are written.

Each finding names its evidence, and that was checked rather than assumed: `scripts/helper.js` for SF9002,
`google.github.io` for SF9004, "allows 500 lines and this skill has 524" for SF9007.

## Measured against real skills

Run over 32 skills installed on a working machine (2026-07-27), the rules behaved like this:

| Code | Skills affected |
|---|---|
| SF1010 — no compatibility declared | 32 of 32 |
| SF1009 — no license declared | 30 of 32 |
| SF1002 — description states no activation context | 3 |
| SF1003 — `SKILL.md` over 1000 lines | 0 |

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
