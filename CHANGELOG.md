# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Versions are `YY.DayOfYear.Build` — `26.208.1` is the first build on 27 July 2026 — which is valid SemVer but
carries no promise about compatibility from its shape. Breaking changes are called out in the notes instead.
Roadmap milestone names ("v0.1.0 — Local Validator") label scope, not releases; see `docs/architecture.md`.

## [Unreleased]

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
