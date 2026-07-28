# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Versions are `YY.DayOfYear.Build` — `26.208.1` is the first build on 27 July 2026 — which is valid SemVer but
carries no promise about compatibility from its shape. Breaking changes are called out in the notes instead.
Roadmap milestone names ("v0.1.0 — Local Validator") label scope, not releases; see `docs/architecture.md`.

## [26.209.2] — 2026-07-28

### Changed — the NuGet package page

- **The package no longer ships the repository README.** nuget.org renders neither relative links nor relative
  images, so two releases went out with a broken logo and a dozen dead `docs/...` links on the package page.
  Nothing failed and nothing warned; the page just looked neglected. `src/SkillForge.Cli/PACKAGE.md` replaces it,
  with absolute URLs throughout, and answers what a package page is actually asked — how to install this and what
  to type — rather than how to build the repository from source.
- `PackageProjectUrl` and `RepositoryUrl` are set. Without them the page showed no repository or project link at
  all.
- The `Description` says what the tool does instead of what it is; it is the one sentence shown in search results.
- Tags widened to the terms somebody would actually search: `agent-skills`, `claude-code`, `mcp`, `dotnet-tool`,
  `linter`.
- A test guards it: every link in `PACKAGE.md` must be absolute, the install command must be present, and every
  shipped command must be named. A page that lists five of seven commands is worse than one that lists none,
  because a reader believes it.

## [26.209.1] — 2026-07-28

### Added — `skillforge migrate inspect`

Reports the agent tooling installed on a machine, per provider: skills, MCP servers and instruction files. It
describes and does not judge, like `inspect`, and always exits `0`. A provider that is not installed is still listed
as absent, because that absence is the answer to "can I move to it?".

- One `IAgentToolAdapter` per provider (`claude-code`, `codex`, `cursor`, `github-copilot`), each stating its own
  provider's paths and nothing else. The inspector that runs them knows no path at all, so a provider that moves a
  file is a one-class diff — the seam the MCP protocol adapters will use next.
- `IMcpConfigurationReader` declares which **format** it handles, not which provider wrote the file: JSON for Claude
  Code, Cursor and VS Code, TOML for Codex.
- `--user-directory` reads an exported profile instead of the current user's home. `--format json` for scripts; no
  SARIF, because an inventory is not a set of findings.
- SF1015 when a provider's configuration exists but cannot be parsed. The rest of the inventory is still reported —
  skipping the file would make an incomplete inventory look complete, the same reasoning as SF1012 and SF1014.

**Environment variable values are never read.** `McpServerDeclaration` has no field for them: the readers take the
names out of `env` and drop the values. An MCP declaration is one of the likeliest places in a home directory to hold
a token, and filtering on the way out would be one refactor away from a leak — having nothing to filter is not. Two
tests assert a known secret value appears nowhere in the console output or the JSON. `~/.claude/.credentials.json` and
`~/.codex/auth.json` are never opened.

Verified against a real machine: 32 skills across 34 directories (two hold no `SKILL.md`), three MCP servers from
`~/.claude.json` and one from `~/.codex/config.toml`, env names only. Two facts came out of looking rather than
reading — `~/.copilot/config.json` is JSON **with `//` comments**, so a strict parser calls a working configuration
corrupt; and Codex is the only provider using TOML, which is why `Tomlyn` is now a dependency (justified in
`docs/architecture.md`).

Two of the roadmap's five asks are deliberately absent, with reasons recorded in `docs/migration.md`: "conflicting
instructions" is a judgement about prose, and "missing dependencies" cannot say "not on this PATH" without implying
"broken". Copilot's and Cursor's skill directories are not guessed at either — an invented path yields a heading that
is permanently empty for the wrong reason.

### Added — `SF7xxx`, provider compatibility

- Provider profiles behind `IAgentProviderRegistry`, one file per provider: `claude-code`, `codex`, `cursor`,
  `github-copilot`. This is the adapter seam v0.4 needs — `migrate inspect` asks the same question of a wider set,
  and MCP adapters will sit beside these rather than inside the rules.
- SF7001: compatibility is declared with a provider SkillForge does not recognise. When the identifier is a near
  miss it names the one it was probably meant to be and offers the replacement as a fix — `claude_code`,
  `ClaudeCode`, `claude-cod` and `copilot` all resolve. When two known identifiers are equally close it suggests
  neither: naming one of two would be a coin toss presented as advice.
- SF7002 / SF7003: the `name` or `description` is longer than a declared provider accepts.
- `validate --provider claude-code,codex` — check against providers the skill does not declare, without editing it
  to find out. Unlike `--suppress`, an unrecognised value is a finding rather than a usage error, because it may be
  a real provider SkillForge has not learned yet.

**Nothing is checked against a provider the skill does not name.** Measured on 229 real skills, plain `validate`
produces **0** SF7xxx findings — and that zero proves nothing on its own, because none of the 229 declares
`compatibility` at all, so the checks never ran. Saying so is the point. `--provider claude-code` on the same corpus
produces **1**: a description of 1064 characters against a documented limit of 1024, verified by parsing the
frontmatter independently, in a skill actually installed in Claude Code.

Only `claude-code` carries documented limits. The other three profiles declare none and therefore check nothing —
an unread limit is left unset rather than guessed at, and unset is not "no limit". They are still in the registry
because recognising the identifier is what stops a legitimate `compatibility: [codex]` being reported as a typo.

A fourth code — "the skill uses a capability a declared provider does not support" — was designed and **dropped**:
no documented fact per provider was available to drive it, and a rule with no data behind it either never fires or
reports a constraint that may not exist.

### Changed

- The provider checks are not `ISkillValidationRule`s. A rule sees the skill and nothing else, and these also depend
  on `--provider`; threading run options through all twenty rules to serve three of them would be the wrong trade.
  They are merged into the report where the loader's diagnostics already are, so suppression, ordering, JSON and
  SARIF apply unchanged. SF6001, reported by `diff`, set the precedent.
- `--suppress` accepts `SF7xxx`. The validating regex stopped at `SF6xxx`, so a new band would have been reported
  and then refused suppression.

## [26.208.2] — 2026-07-27

### Added — `SF4xxx`, instruction injection in the body

- `MarkdownProse` — reduces a body to the lines a human reads as prose: fenced code blocks dropped, inline code
  spans removed, nothing else filtered. It exists because a measurement demanded it, not for tidiness.
- SF4001: the body's prose tells the agent to set aside or override the instructions it was given.
- SF4002: the body's prose tells the agent to keep something from the user.

This is the job SF3002 was measured out of, done with two independent defences against the failure that stopped
it. SF3002 scanned raw bodies and produced 12 findings on 229 real skills with roughly one real; the failures were
code shown to a reader — a YAML comment `# Ignore other fields`, a security skill's own detection pattern as a
string literal. So these rules read prose rather than text, **and** every pattern requires the noun it is about
(`ignore … other instructions`, not `ignore … other`). Either defence alone catches one of those two; together
they catch both.

Measured on the same 229 skills: **2 findings, both SF4001, both real** — two skills that state they override the
agent's default behaviour. SF4002 fires zero times, which proves the absence of false positives and nothing about
its value; that rests on crafted positives, as with SF3xxx.

Credential-file access and exfiltration patterns were written and then **not** shipped: no measurement supported
either shape, and D-29 forbids publishing a rule on a guess about its firing rate. They are candidates for
`SF5xxx`.

### Added — `SF5xxx`, supply chain

- SF5001: the skill fetches something remote from a reference that can change — a branch, a package or image
  resolved to `latest`, a latest-release download. Reads the body **including code blocks**, plus every script,
  because to this question a fenced block is the install command the agent will run, not an example being shown.
  That is the opposite of SF4001's choice, and deliberately so.

Measured on 229 real skills, and the measurement changed the rule. Matching the version selector alone gave four
findings, one of which was a skill *recommending* pinned tags, with the rule firing on the counter-example it
cited — "Use specific version tags (node:22-alpine, not node:latest)". Markdown structure could not separate that
case: the false positive is inside a fence and a true positive is inside an inline span in a bullet list.
Requiring a fetch verb on the line could: **3 findings, 0 false positives.** The measured false positive is now a
test.

Provenance ("no source declared, so the origin cannot be checked") was considered and **not** shipped: it would
fire on approximately every skill, and SF1009 and SF1010 already occupy that shape.

### Added — `skillforge eval`, milestone v0.3

- `skillforge eval <path>` checks a skill against the expectations its author declared under `evals/*.yaml`:
  required files, a required or forbidden shell permission, diagnostic codes to forbid or to pin, terms the
  description must mention, and vocabulary-overlap cases. Console or JSON output; exit 0 all held, 1 one did not.
- SF1014: an `evals` file that cannot be read or parsed is reported and skipped rather than failing the run — the
  same choice SF1012 makes for `skillforge.yaml`.
- `samples/dotnet-api-review/evals/eval.yaml` is the worked example, and CI runs it.

Three deliberate asymmetries. A skill with **no** `evals` folder exits 0 saying there is nothing to run; a skill with
an **empty** suite fails, because an author who made the folder and wrote no cases should not be told everything is
fine. A case that asserts nothing is *skipped*, not passed — counting it would make a suite look larger than it is.
`expect` exists so an author who has accepted a finding can pin it instead of fixing it to stay green.

**The `activation` cases report shared vocabulary and refuse to call it activation.** Whether an agent chooses a skill
is decided by a model reading a whole prompt and a whole toolbox; SkillForge sends nothing to a model, so it cannot
answer that. What it can answer is a necessary condition — an agent that never sees the skill's vocabulary has nothing
to match on — so a failure is informative while a pass only means the skill is not disqualified on wording. The output
never says "would fire". Real activation testing needs a model runner, which is a separate thing with an honest name.

A length filter alone was tried for the overlap check and failed: "Use this skill when tuning a database index" and
"translate this paragraph into Turkish" share **"this"**, enough to make two unrelated sentences look related. A
stop-word list was therefore necessary, and it is **English only** — a limitation stated rather than hidden.

### Added — `SF6xxx`, version and evolution

- SF6001: the skill's reach grew while its declared version stayed the same, so anyone pinned to that version
  received the change without being told. Reported by `diff`, because it needs two revisions — it is not a
  validation rule and could not be one.

It requires a version on both sides. An unversioned skill promises nothing, so it breaks nothing, and a version
appearing for the first time said nothing about the revision before it.

"No version is declared" is deliberately **not** a rule: measured at 210 of 229 real skills, 91%. Same shape as
SF1009 and SF1010, and the same reason provenance was rejected from SF5xxx — the both-sides requirement is what
keeps it from arriving through the back door.

### Changed — reports say how to fix things, not just what is wrong

- A finding whose resolution is one known edit now carries a `Fix`: the literal text to paste. It prints **without**
  `--verbose`, because making somebody pass a flag to learn how to solve a one-line problem tells them what is wrong
  and leaves them to work out the schema. `--verbose` still prints the reasoning, which is what it was for.
- SF1006's fix names the interpreters inferred from the skill's own scripts (`allowed: [bash, node]` from the `.sh`
  and `.js` files it ships). A guess from extensions, presented as one — a reader confirms or corrects it in a
  second, where an empty list makes them open four files first.
- SF1009 and SF1010 carry one-line frontmatter fixes.
- A `Next:` footer closes a report: how many findings have a fix, and the exact `--suppress` flag for the rules that
  fire on almost every skill. Only codes that actually fired are named; `--quiet` drops it. Batch runs print it once
  for the whole run, not once per skill.
- JSON output gained a `fix` field per diagnostic. Additive, so the schema version is unchanged. **SARIF does not
  carry it** — SARIF's `fixes` describes precise artifact edits, and a paste-this-in snippet is not one.

### Fixed

- SF1013: a `version` written at the top level of the frontmatter was **discarded in silence**. The schema keeps it
  under `metadata`, and every other field a skill declares is top-level, so it is an easy mistake — one that meant
  SF0010 never checked the version, `inspect`, `pack` and `diff` reported none, and SF6001 could not fire at all.
  SkillForge now reads it *and* says where it belongs; an explicit `metadata.version` wins if both are present.
  Found by writing a fixture that way and believing the tool.


- SF1006 pointed at `skillforge.yaml` even for the majority of skills that have no such file. A reader followed the
  location to an empty path, and a SARIF consumer annotated a file outside the repository, so GitHub dropped or
  misplaced the annotation. It now points at the configuration file when one exists and at `SKILL.md` otherwise,
  matching SF1009 and SF1010. The suggestion still names the file to create. Found in real output.

## [26.208.1] — 2026-07-27

### Added — milestone v0.1.0, Local Validator

- `skillforge init` — scaffolds a skill that passes `validate` with no findings.
- `skillforge inspect` — file inventory, external URLs, inferred capabilities and declared tools, as
  console text or JSON. Describes; never claims a security verdict.
- `skillforge pack` — deterministic `.skill.zip` plus a `.sha256` in `sha256sum -c` format and a manifest.
  Validation is a gate; `--skip-validation` is explicit and printed when used.
- `--format json` and `--format sarif` on `validate`, with `--output` to write to a file.
- SARIF 2.1.0 output with rule declarations, severity mapping and repository-relative locations, so
  findings appear as pull request annotations.
- Global tool packaging: `dotnet tool install --global SkillForge.Cli` installs `skillforge`.
- Docs: `docs/cli-reference.md`, `docs/ci.md`, `docs/skillforge-manifest-rfc.md`.
- CI runs the built CLI over the sample skills, including asserting that the broken sample exits 1.

- Logo and brand assets under `assets/`: horizontal lockup for light and dark backgrounds, the mark on its
  own, a simplified cut for favicon sizes, and a rasterised 128×128 PNG used as the NuGet package icon.
  The mark is an anvil, deliberately not a badge — a shield or tick would promise the safety verdict
  SkillForge refuses to give (ADR-006). Rationale and usage rules are in `assets/README.md`.

- Batch validation: `skillforge validate <directory>` now validates every skill under a directory, at any
  depth, replacing the shell loop the CI documentation used to recommend. JSON nests the skills; SARIF merges
  them into one run so a single upload covers the whole repository. One bad skill fails the run.

- Rule configuration: `--suppress SF1009,SF1010` on the command line, and a `validation` section in a skill's own
  `skillforge.yaml` (`strict`, `suppress`). The two add up rather than overriding each other. Suppressed findings
  are always counted and the count is always shown, so a shrunken report never looks like a clean one. A
  `skillforge.yaml` that cannot be parsed is ignored with SF1012 rather than failing the run — this is the first
  part of that file SkillForge actually honours.
- SF1011: a reference pointing at a sibling skill is now a warning rather than an SF0008 error. Measured on 229
  real skills, 21 of 21 such "errors" were legitimate cross-references inside one collection.

- `skillforge diff <before> <after>` — the behaviour-surface diff. Reports permissions, hosts, scripts, files,
  compatibility, name, version and description changes, plus which findings are new versus resolved, and leads with
  the three changes that widen a skill's reach. Exits 1 on a new error; `--fail-on-change` makes any change fail.
  It deliberately does not claim an activation scope became "broader" — that cannot be judged honestly from text.

### Added — milestone v0.2.0, activation and supply-chain risk

- SF3001 and SF3002, the first two rules in the activation-risk band: a description claiming universal
  applicability, and activation text aimed at the agent's decision rather than the reader's understanding.
  Both read the description only, and both are warnings that say outright they are not calling the skill
  malicious (ADR-006). SF3002 read the body as well until it was measured: 16 findings on 229 real skills,
  roughly one genuine — the rest ordinary English, including one security skill's own detection pattern in a
  string literal. Body scanning and the weakest pattern were dropped; the same 229 skills now produce 5
  findings. Detecting injected instructions inside a body belongs to the reserved `SF4xxx` band.
- A composite GitHub Action at the repository root: `uses: NetAnlatAkademi/skillforge@<ref>`. Runs `validate`,
  writes SARIF and uploads it to code scanning, so findings arrive as inline pull-request annotations. Inputs
  for `path`, `strict`, `suppress`, `sarif-file`, `upload-sarif` and `version`; `exit-code` as an output.
  Leaving `version` empty builds the CLI from the action's own checkout, so the action works before the tool
  is published on NuGet. A second workflow exercises both of its outcomes on the sample skills.

### Changed

- Versions are now `YY.DayOfYear.Build`, computed in `Directory.Build.props` from UTC with `SKILLFORGE_BUILD`
  overriding the build number. `26.208.1` rather than an invented `0.1.0`: the shape says when it was built
  and promises nothing about compatibility, which is honest for a tool with no released contract yet.
- Code-standards pass over the whole codebase: one type per file, `= default` on every cancellation-token
  parameter, cancellation checked once per rule by the validator rather than in each rule, magic values
  turned into constants, dead code removed, and synchronous console writes inside async methods awaited.
- `SarifReportSerializer` no longer re-scans the diagnostic list once per rule code.
- Test suite grown from 373 to 399: `init`, `inspect` and `pack` command runners now have exit-code tests,
  `SkillName` is tested directly, and the five placeholder bootstrap tests are gone.

### Added — foundations

- Repository bootstrap: solution with five source projects and five xUnit test projects.
- Repository-wide build settings (`Directory.Build.props`): .NET 10, nullable reference types,
  warnings as errors, XML documentation.
- Central Package Management (`Directory.Packages.props`).
- `.editorconfig` with C# formatting and naming conventions.
- GitHub Actions CI workflow running restore, format verification, build and test on Linux and Windows.
- Initial architecture document (`docs/architecture.md`) and diagnostic code registry
  (`docs/validation-rules.md`).
- `NuGet.config` restricting restore to nuget.org so builds are reproducible.
- Skill loader: reads a skill from a directory or a `SKILL.md` path into a `SkillDefinition`, splitting
  YAML frontmatter from the Markdown body and inventorying the surrounding files.
- Domain model: `SkillDefinition`, `SkillFrontmatter`, `SkillResource`, `Diagnostic`,
  `DiagnosticSeverity`, `DiagnosticCodes` (all 24 codes) and `OperationResult<T>`.
- `IFileSystem` and `IFrontmatterParser` abstractions, implemented in Infrastructure over
  `System.IO` and YamlDotNet.
- `SkillPathGuard`: rejects paths and symbolic links that resolve outside the skill directory (SF0008).
- Loader diagnostics SF0001, SF0002, SF0003, SF0008 and SF0009. Malformed YAML produces a diagnostic
  with a line number instead of an exception, and an unreadable `SKILL.md` produces SF0001 rather than
  propagating an I/O exception.
- Four sample skills under `samples/` used as test fixtures: `valid-skill`, `invalid-frontmatter`,
  `broken-references` and `dotnet-api-review`.
- Validation engine: `ISkillValidationRule`, `SkillValidator`, `ValidationReport`, `ValidationSummary`
  and deterministic diagnostic ordering. A rule reporting an error does not stop the others.
- Eleven validation rules covering SF0004, SF0005, SF0006, SF0007, SF0008 (body references), SF0010,
  SF1001, SF1002, SF1003, SF1009 and SF1010.
- `MarkdownLinkExtractor` and `SkillRelativePath`: find the local file references in a skill's body,
  ignoring external URLs, anchors and fenced code blocks.
- `coverlet.runsettings` excluding source-generated code from coverage.
- Working CLI: `skillforge validate <path>` with `--strict`, `--quiet`, `--verbose` and `--no-color`,
  plus help and version. Exit codes are 0 clean, 1 validation failure, 2 usage error, 3 unexpected.
- Console report renderer: errors first, summary last, every finding tagged with its code and a text
  marker so output survives without colour. `NO_COLOR` is honoured.
- Composition root wiring every layer, validated at build so a missing registration cannot reach a user.
