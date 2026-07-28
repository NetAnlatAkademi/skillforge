# CLI reference

```text
skillforge [global options] <command> [arguments] [options]
```

Global options work before or after the command:

| Option | Effect |
|---|---|
| `--quiet`, `-q` | Print only errors and the final verdict |
| `--verbose` | Print the reasoning behind each finding. The *fix* is printed without it — see below |
| `--no-color` | Disable colour. The `NO_COLOR` environment variable does the same |
| `--help`, `-h` | Show help |
| `--version` | Show the tool version |

Exit codes are documented in [ci.md](ci.md).

---

## `skillforge init`

Creates a new skill from a template. The generated skill passes `validate` with no findings — that is
asserted by a test, so a template change cannot quietly start producing warnings.

```bash
skillforge init my-skill
skillforge init my-skill --description "Reviews ASP.NET Core APIs" --author "Çağrı" --license MIT
skillforge init my-skill --directory ./skills/my-skill
```

| Option | Default | Effect |
|---|---|---|
| `--directory`, `-d` | a directory named after the skill | Where to create it |
| `--description` | a placeholder that states an activation context | Frontmatter description |
| `--author` | omitted | Recorded under `metadata.author` |
| `--license` | `MIT` | SPDX identifier |
| `--force` | off | Overwrite an existing skill |

Creates `SKILL.md`, `skillforge.yaml`, and `references/`, `scripts/`, `assets/`, `evals/` — each with a
README, because git does not track empty directories.

Without `--force`, a directory that already contains a `SKILL.md` is refused with exit code 2. The check
happens before anything is written.

---

## `skillforge validate`

Validates a skill against the rules in [validation-rules.md](validation-rules.md).

```bash
skillforge validate ./my-skill
skillforge validate ./my-skill --strict
skillforge validate ./my-skill --format json --output artifacts/report.json
skillforge validate ./my-skill --format sarif --output artifacts/skillforge.sarif
```

### Validating many skills at once

Point `validate` at a directory that holds several skills and it validates all of them:

```bash
skillforge validate ./skills
skillforge validate ./skills --format sarif --output artifacts/skillforge.sarif
```

```text
SkillForge Validate

Root:   ./skills
Skills: 4

broken-references
x SF0007 The referenced file 'references/checklist.md' does not exist in the skill. (SKILL.md:16)

dotnet-api-review
  ok

Result: INVALID — 2 of 4 skills have errors
Errors: 3  Warnings: 1  Info: 0
```

How the two modes are told apart: a path that is a file, or a directory with its own `SKILL.md`, is **one**
skill — even when it contains others, because a nested `SKILL.md` is far more likely to be a fixture the
outer skill ships than a second skill. Anything else is searched for skills, at any depth, skipping tooling
directories (`bin`, `obj`, `.git`, `node_modules`, `artifacts`, `dist`). A directory with no skills in it
reports SF0001, rather than quietly passing because it found nothing.

**One bad skill fails the run.** A batch that passed because most of its skills were fine would be useless as
a build gate.

Machine-readable output changes shape for a batch, and each format does what its consumer expects:

- **JSON** nests each skill under `skills`, each with its own summary and diagnostics, plus a run-level
  summary. The single-skill document is unchanged, so an existing consumer keeps working.
- **SARIF** merges everything into **one run**, so a single file upload covers every skill in the
  repository. Each result keeps its own skill-relative path, so annotations still land on the right files.

| Option | Default | Effect |
|---|---|---|
| `path` | `.` | One skill, or a directory of skills |
| `--strict` | off | Treat warnings as failures. Measured on 32 real skills, this fails all of them — see [validation-rules.md](validation-rules.md#measured-against-real-skills) before adopting it as a CI gate |
| `--format`, `-f` | `console` | `console`, `json` or `sarif` |
| `--output`, `-o` | stdout | Write machine-readable output to a file |
| `--suppress` | nothing | Codes not to report, comma-separated or repeated. A value that is not a code is a usage error, because a typo would otherwise silently suppress nothing |
| `--provider` | nothing | Also check against these agent providers, comma-separated or repeated, even when the skill does not declare them |

### Checking against an agent provider

A skill is checked against the providers it declares under `compatibility`, and against nothing else. `--provider`
asks the other question — "would this work on Claude Code?" — without editing the skill to find out:

```bash
skillforge validate ./skills --provider claude-code
skillforge validate ./skills --provider claude-code,codex
```

The providers SkillForge recognises are `claude-code`, `codex`, `cursor` and `github-copilot`. An identifier it does
not recognise is reported as **SF7001** rather than rejected as a usage error, because it may be a real provider
SkillForge has not learned yet — and when it is a near miss, the finding names the identifier it was probably meant
to be:

```text
! SF7001 Compatibility is declared with 'claude_code', which SkillForge does not recognise, so nothing was
         checked against it. (SKILL.md:1)
         fix:  in 'compatibility', replace 'claude_code' with 'claude-code'
```

**SF7002** and **SF7003** compare the `name` and `description` against the limits a declared provider documents.
Only `claude-code` has documented limits in SkillForge today (64 and 1024 characters); the other three are
recognised but declare none, and an unread limit is never checked rather than guessed at. See
[validation-rules.md](validation-rules.md#provider-compatibility) for the profile table and the measurements.

### Suppressing rules

Two places, and they add up rather than overriding each other — a repository-wide flag and a per-skill decision
are different decisions:

```bash
skillforge validate ./skills --suppress SF1009,SF1010
skillforge validate ./skills --suppress SF1009 --suppress SF1010   # same thing
```

```yaml
# skillforge.yaml, in the skill's own directory
validation:
  strict: false
  suppress:
    - SF1009
```

Anything can be suppressed, errors included: a repository that has decided a rule does not apply to it has a
reason SkillForge cannot see. What keeps it honest is that the count is **always** reported —
`Suppressed: 2` on the console, `summary.suppressed` in JSON — so a shrunken report never looks like a clean one.

`skillforge.yaml` is optional and its absence is never a finding. A file that exists but cannot be parsed is
ignored with **SF1012** rather than failing the run: punishing a typo in an optional file would be worse, and
ignoring it silently would let a suppression the author wrote quietly not apply.

When `--output` is used, the human-readable report still goes to the console: a CI log that says nothing
about why a build failed is not much use.

A skill that cannot be loaded at all still produces a report, in the same shape as any other failure.

---

## `skillforge inspect`

Summarises what a skill contains and what its contents imply it can do.

```bash
skillforge inspect ./my-skill
skillforge inspect ./my-skill --format json
skillforge inspect ./my-skill --show-permissions
```

| Option | Default | Effect |
|---|---|---|
| `path` | `.` | Skill directory, or a `SKILL.md` path |
| `--format`, `-f` | `console` | `console` or `json` |
| `--output`, `-o` | stdout | Write to a file |
| `--show-files` | all sections | Only the file inventory |
| `--show-links` | all sections | Only external URLs |
| `--show-permissions` | all sections | Only inferred capabilities and declared tools |

Inspect describes; it does not judge. A skill that ships a script and three URLs still exits 0. Capabilities
are inferred from the contents (`Filesystem Read`, `Shell Execution`, `Network Access`, `Binary Content`) and
the output says outright that it is not a security verdict.

URLs are read from `SKILL.md` only. Scanning every referenced file is a fuller answer and belongs to the
security-signals milestone; until then the limitation is stated rather than implied away.

---

## `skillforge diff`

Compares two versions of a skill by **what they can do**, not by which bytes changed.

```bash
skillforge diff ./before ./after
skillforge diff ./before ./after --format json --output artifacts/diff.json
skillforge diff ./before ./after --fail-on-change
```

```text
SkillForge Diff

Before: ./before
After:  ./after

The skill can now do more than before:
  Permissions added:
    shell.execute

  Scripts added:
    scripts/analyze.ps1

  Domains added:
    api.example.com

Version: 1.0.0 -> 1.1.0

Description changed:
  before: Use this skill when reviewing an API before it ships.
  after:  Use this skill when reviewing an API, a database or any deployment.
```

| Option | Default | Effect |
|---|---|---|
| `before` | — | The earlier version: a skill directory or a `SKILL.md` path |
| `after` | — | The later version |
| `--format`, `-f` | `console` | `console` or `json` |
| `--output`, `-o` | stdout | Write to a file |
| `--fail-on-change` | off | Fail on any surface change, not only on a new error |

Exit codes: `1` when either side cannot be loaded, or when the later version has a **new error** — that is a
regression by any definition. Otherwise `0`, because whether a new permission is acceptable is a policy SkillForge
does not own; `--fail-on-change` is how a caller says a change alone should stop a build.

### What it compares, and what it refuses to guess

Reported: declared permissions, agent compatibility, **hosts** the entry point points at, scripts, every file,
name, version, description, and which findings are new versus resolved. Findings are matched by code and location
rather than by message, so rewording a diagnostic does not read as one finding resolved and another appearing.

Hosts rather than URLs: a link moving from `/docs/a` to `/docs/b` on the same host is not a change in *who* the
skill talks to, which is the question a reviewer is actually asking.

**It does not claim the activation scope became "broader".** Judging that from a description's text is not
something that can be done honestly — a shorter description can match more, a longer one can match more, and which
words matter depends on the agent. The description change is shown in full so a human can judge it. Actually
testing activation is what evals are for (v0.3).

### Comparing two git revisions

Not built in yet: `diff` takes two paths. Until it does, git can produce the two paths:

```bash
git worktree add ../skillforge-base origin/main
skillforge diff ../skillforge-base/skills/my-skill ./skills/my-skill
git worktree remove ../skillforge-base
```

Taking a revision range directly needs SkillForge to run `git`, which is a capability — and a set of failure modes
— it does not have today. The workaround above is what the eventual built-in support will do underneath.

## `skillforge pack`

Packages a skill into a deterministic archive with a hash and a manifest.

```bash
skillforge pack ./my-skill
skillforge pack ./my-skill --output ./dist
skillforge pack ./my-skill --version-override 1.0.0
```

| Option | Default | Effect |
|---|---|---|
| `path` | `.` | Skill directory, or a `SKILL.md` path |
| `--output`, `-o` | `artifacts` | Directory to write to |
| `--version-override` | the skill's `metadata.version` | Version to package as |
| `--skip-validation` | off | Package even when validation finds errors |

Produces three files:

```text
artifacts/
├── my-skill.1.0.0.skill.zip
├── my-skill.1.0.0.skill.zip.sha256
└── my-skill.1.0.0.manifest.json
```

The archive is deterministic — entries sorted, timestamps pinned, paths always using `/` — so the same
contents produce the same hash on any machine. The `.sha256` file uses the format `sha256sum -c` expects.

Validation is a gate. A skill with errors is not packaged unless `--skip-validation` is passed, and when it
is, the CLI says so rather than doing it silently. Tooling directories (`bin`, `obj`, `.git`, `node_modules`,
`artifacts`, …) are never included.

## What a report tells you

A finding whose resolution is a single known edit prints that edit, and prints it **without** `--verbose`:

```text
! SF1006 The skill ships 4 scripts (scripts/helper.js, scripts/server.cjs,
scripts/start-server.sh, scripts/stop-server.sh) but declares no shell permission. (SKILL.md:1)
    fix  create skillforge.yaml:
           permissions:
             shell:
               allowed: [bash, node]
! SF1009 No license is declared. (SKILL.md:1)
    fix  add to the frontmatter:  license: MIT

Result: VALID WITH WARNINGS
Errors: 0  Warnings: 4  Info: 0

Next: 3 of these have a fix printed above.
      SF1009 and SF1010 fire on almost every skill. If they do not
      apply here, run with:  --suppress SF1009,SF1010
```

Three things are deliberate here.

**A fix is not behind a flag.** Making somebody pass `--verbose` to learn how to resolve a one-line problem tells
them what is wrong and leaves them to work out the schema. `--verbose` still exists, and still prints the prose
about *why* a rule fired — that is reasoning, not a paste-able edit.

**Most findings have no fix, and that is not an omission.** SF1007 points at a script that reaches further than
usual; only a human can decide what should replace it. Inventing a fix there would be worse than silence.

**SF1006's fix names interpreters inferred from the skill's own scripts** — `[bash, node]` above came from the
`.sh` and `.js` files. It is a guess from file extensions, and it is presented as one: a reader can confirm or
correct it in a second, whereas an empty list makes them open four files first.

The `Next:` footer says how much of the list is trivially fixable, so four warnings are not read as four problems,
and hands over the exact `--suppress` flag for SF1009 and SF1010. Those two fire on approximately every real skill.
They were kept because they are correct — see [validation-rules.md](validation-rules.md) — which is precisely why
the report should give a first-time reader the escape hatch instead of burying it in documentation. Only codes that
actually fired are named. `--quiet` drops the footer.

In JSON output the same text is available as a `fix` field on each diagnostic. It is additive, so the schema version
is unchanged and a consumer that does not know the field ignores it. **SARIF deliberately does not carry it**: SARIF's
`fixes` property describes precise artifact edits, and a paste-this-in snippet is not one.

## `skillforge eval`

Checks a skill against the expectations its author wrote down under `evals/`. A regression harness: it asks whether
the skill still looks the way it was declared to look.

```bash
skillforge eval ./skills/dotnet-api-review
skillforge eval ./skills/dotnet-api-review --format json --output eval.json
```

Cases live in `evals/*.yaml`, merged in file-name order so a run is reproducible:

```yaml
cases:
  - name: keeps the reference it points at
    files:
      - references/api-versioning.md

  - name: never regains a broken reference
    forbid: [SF0007, SF0008]

  - name: keeps its accepted finding pinned
    expect: [SF1009]

  - name: declares what it runs
    shell: required        # or: forbidden

  - name: stays about API review
    mentions: [API, review]

  - name: a security review request shares its vocabulary
    activation:
      prompt: review my ASP.NET Core API before it ships
      overlap: true        # default; use false for a prompt that should share nothing
```

`expect` exists so an author who has deliberately accepted a finding can pin it, rather than having to fix it to keep
their evals green.

Exit codes: `0` every case held · `1` at least one did not · `2` usage error. A skill with **no** `evals` folder exits
`0` and says there is nothing to run; a skill with an empty suite **fails**, because an author who made the folder and
wrote no cases should not be told everything is fine. A case that asserts nothing is reported as skipped rather than
passed — counting it would make a suite look larger than it is. An `evals` file that cannot be parsed is reported as
SF1014 and skipped, the same choice SF1012 makes for `skillforge.yaml`.

### What `activation` does and does not do

**It is not an activation test, and calling it one would be a lie.** Whether an agent chooses a skill is decided by a
model reading a whole prompt, a whole toolbox and a whole conversation. SkillForge sends nothing to a model, so it
cannot answer that and does not pretend to.

What it checks is a **necessary condition**: an agent that never sees the skill's vocabulary in the prompt has nothing
to match on. So a failure is informative — the description is missing the words — while a pass proves only that the
skill is not disqualified on vocabulary. The report is phrased as "shares wording with" throughout and never says
"would fire".

Two limits worth knowing. Words of three characters or fewer are dropped, and so is a list of common English function
words — a length filter alone was tried first and failed, because "Use this skill when tuning a database index" and
"translate this paragraph into Turkish" share **"this"**, which was enough to make two unrelated sentences look
related. That stop-word list is **English only**, so a Turkish or German description will share function words this
check does not recognise and its overlap result will be correspondingly generous.

Real activation testing needs a model runner. That is a separate thing to build, and it needs an honest name.

---

## `skillforge migrate inspect`

Reports the agent tooling installed on this machine, per provider: skills, MCP servers and instruction files.

```bash
skillforge migrate inspect                       # user scope only
skillforge migrate inspect .                     # plus this project's configuration
skillforge migrate inspect . --format json
skillforge migrate inspect --user-directory /exported/profile
```

| Option | Default | Effect |
|---|---|---|
| `project` | none | Project directory to include project-scoped configuration from. Without it, only user scope is read |
| `--user-directory` | the current user's home | Read this directory instead — an exported profile, or a fixture |
| `--format`, `-f` | `console` | `console` or `json`. **No SARIF**: an inventory is not a set of findings, and dressing one up as findings would misuse the format |
| `--output`, `-o` | stdout | Write to a file |

It **describes and does not judge**, like `inspect`, and always exits `0`. A provider that is not installed is still
listed, as absent — that absence is the answer to "can I move to it?", and leaving it out would look like it was
never looked for.

**Environment variable values are never read or printed** — only the names a declaration sets. The model has no field
for a value, so there is nothing to filter and nothing to leak. `~/.claude/.credentials.json` and `~/.codex/auth.json`
are never opened at all.

A configuration file that exists but cannot be parsed is reported as **SF1015** and the rest of the inventory is still
shown, because a silently skipped file makes an incomplete inventory look complete.

`migrate` is a command group with one member on purpose: reading a setup and changing one are different acts, and a
future `migrate apply` must not be reachable by mistyping a flag on the read.

Which paths are read for each provider — and which of them were verified against a real installation rather than taken
from documentation — is in [migration.md](migration.md), along with the two things this command deliberately does not
do yet.

### Asking a model whether it would choose the skill

`eval` runs `model_activation` cases only when the run names a model. Without one they are reported as skipped, with a
line saying so.

```bash
skillforge eval ./my-skill --model qwen3:8b --model-endpoint http://localhost:11434/v1
```

| Option | Default | Effect |
|---|---|---|
| `--model` | none | Model to ask, e.g. `qwen3:8b` or `gpt-5`. Must be given with `--model-endpoint` |
| `--model-endpoint` | none | Base URL of an OpenAI-compatible API. There is no default: SkillForge sends nothing anywhere until told where |
| `--model-api-key-env` | none | **Name** of the environment variable holding the API key. The key is never an argument, so it cannot reach a shell history or a CI log |
| `--max-model-requests` | 100 | Refuse the run, before any request, if the cases need more than this |

The skill is offered to the model alongside its siblings as distractors, each prompt is asked `runs` times at
temperature zero, and the result is reported as `k` of `n` against the threshold the author declared. Model results carry
no diagnostic code and live in their own section — see [model-runner.md](model-runner.md) for why, and for what this
still does not prove.
