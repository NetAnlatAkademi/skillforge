# SkillForge — TODO

Single source of truth for progress. Derived from [SKILLFORGE_ROADMAP.md](SKILLFORGE_ROADMAP.md);
mirrored into the Obsidian vault under `SkillForge/` for cross-session context.

Legend: `[ ]` open · `[x]` done · `[~]` in progress · `[-]` deliberately deferred

Last updated: 2026-07-27

---

## Phase 0 — Bootstrap ✅ complete

- [x] Create git repository (`main` branch)
- [x] Add `.gitignore`
- [x] Add `LICENSE` (MIT)
- [x] Create `README.md` with build and test commands
- [x] Create solution and the ten projects (5 source, 5 test)
- [x] Add `Directory.Build.props` (net10.0, nullable, warnings-as-errors, XML docs)
- [x] Add Central Package Management (`Directory.Packages.props`)
- [x] Enable nullable reference types and warnings-as-errors
- [x] Create xUnit test projects with FluentAssertions 7.x
- [x] Add CI build workflow (`.github/workflows/ci.yml`, Linux + Windows)
- [x] Add code formatting settings (`.editorconfig`, `.gitattributes`)
- [x] Create the initial architecture document (`docs/architecture.md`)
- [x] Verify `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`
- [x] Add `NuGet.config` (not in the roadmap; required because CPM rejects unmapped machine feeds)
- [x] Add `CHANGELOG.md`
- [x] Initial commit
- [x] Create the GitHub remote (`NetAnlatAkademi/skillforge`, private) and push `main`
- [x] Verify the CI workflow green on `ubuntu-latest` and `windows-latest`
- [x] Pin GitHub Actions to `@v5` (the `@v4` versions emit a Node 20 deprecation warning)
- [x] Make the repository public — done once v0.1.0 was usable and the release was tagged

### Deviations from the roadmap

- The solution file is `SkillForge.slnx`, not `SkillForge.sln`. The .NET 10 SDK produces the XML
  solution format by default; it is supported by `dotnet` and Visual Studio 17.14+.
- `NuGet.config` was added. Not listed in the roadmap, but CPM fails restore when the machine defines
  more than one unmapped package source.
- Only test packages are declared in CPM. `System.CommandLine`, `YamlDotNet`, `FluentValidation`,
  `Spectre.Console` and `Microsoft.Extensions.*` are added in the phase that first needs them, so
  Phase 0 ships no unused dependencies.

---

## Phase 1 — Skill Loader ✅ complete

- [x] Domain models: `SkillDefinition`, `SkillFrontmatter`, `SkillResource`, `Diagnostic`, `DiagnosticSeverity`
- [x] `OperationResult<T>` result model
- [x] Diagnostic code constants (`SF0001`…`SF2004`)
- [x] `IFileSystem` abstraction in Application, implementation in Infrastructure
- [x] Skill root detection (accepts a directory or the `SKILL.md` path itself)
- [x] `SKILL.md` discovery
- [x] Frontmatter / body separation (`FrontmatterSplitter`: LF, CRLF, BOM, `...` terminator)
- [x] YAML parsing (`YamlDotNet`, justified in `docs/architecture.md`)
- [x] Markdown body reading
- [x] Resource file enumeration with extension-based classification
- [x] Path normalisation
- [x] Symlink handling (`SkillPathGuard` follows links and rejects escaping targets)
- [x] Loader diagnostics: SF0001, SF0002, SF0003, SF0008, SF0009 — malformed YAML never crashes
- [x] Sample skills: `valid-skill`, `invalid-frontmatter`, `broken-references`, `dotnet-api-review`
- [x] Unit and integration tests — 133 tests; Domain 100%, Application 98.7%, Infrastructure 98% line coverage
- [x] Update `docs/validation-rules.md` and `CHANGELOG.md`

### Hardening pass (TDD, after the first Phase 1 commit)

Two crash paths were found by asking what happens when the file system says no. Both were reproduced as
failing tests first.

- [x] `FileSystem.ResolveLinkTarget` threw `DirectoryNotFoundException` for a path that does not exist.
  The SF0007 rule will ask exactly that question about every broken reference it finds, so a missing
  path — and a dangling link — now answers `null`.
- [x] An unreadable `SKILL.md` (locked, or permission denied) propagated the exception out of the
  loader. It now returns SF0001 with the underlying reason and a permissions hint.
- [x] Pinned an empty `SKILL.md` to SF0002 as a regression test (this already behaved correctly).

Known, accepted risk: a file deleted between enumeration and `GetFileSizeInBytes` would throw. The
window is a single in-process pass over one directory, and guarding it would mean swallowing a genuine
I/O failure, so it is left alone deliberately.

### Decisions taken in this phase

- **The loader only reports what stops a model being built.** SF0004 (`name` missing) and SF0005
  (`description` missing) are validation rules, not loader errors: a skill without a name still loads,
  and `SkillDefinition.Name` is an empty string. This keeps the loader honest about what the file says
  and leaves judgement to Phase 2.
- **SF0009 supersedes SF0003 for a duplicated field.** A duplicate key also makes YamlDotNet fail;
  reporting both would describe one mistake twice.
- **Record equality does not look inside collection members.** `SkillDefinition` compares `Resources`
  and `Metadata` by reference. Anything comparing skills must compare the parts it cares about.

## Phase 2 — Validation Engine ✅ complete

- [x] `ISkillValidationRule` interface — one rule, one code, independently testable
- [x] Rule set and execution pipeline — a rule reporting an error does not stop the others
- [x] Required field rules (SF0004, SF0005)
- [x] Name format rules (SF0006)
- [x] Description quality rules (SF1001, SF1002)
- [x] File reference rules (SF0007) — Markdown links in the body checked against the file inventory
- [x] Path traversal rules (SF0008) — body references that escape the skill directory
- [x] Length rules (SF1003)
- [x] License and compatibility warnings (SF1009, SF1010)
- [x] Package version rule (SF0010)
- [x] Summary calculation (`ValidationSummary`)
- [x] Strict mode (`ValidationReport.HasFailed(strict)`)
- [x] Deterministic diagnostic ordering (`DiagnosticOrdering`, stable)
- [x] 266 tests; line coverage Domain 92.3%, Application 99.6%, Infrastructure 98.1%
- [x] `coverlet.runsettings` so source-generated code stops hiding real coverage gaps

### Decisions taken in this phase

- **A rule that throws is a bug and is not swallowed.** "One failing rule must not stop the others"
  means a rule *reporting an error* does not short-circuit the run. An exception is different: it means
  the rule itself is broken, and hiding it would hand the user a quietly incomplete report. It surfaces
  as exit code 3. This also keeps the codebase free of `catch (Exception)`.
- **Precedence between codes**, so one mistake yields one finding: SF0004 suppresses SF0006; SF0005
  suppresses SF1001 and SF1002; SF0008 suppresses SF0007 for the same reference.
- **SF0007 compares paths case-sensitively on every platform.** `References/Notes.md` for a file named
  `references/notes.md` works on Windows and breaks on Linux; reporting it everywhere names the
  portability bug instead of deferring it.
- **Links inside fenced code blocks are ignored.** They are examples shown to the reader, not files the
  skill depends on.
- **An explicit rule list, not assembly scanning.** Reflection would make the active rule set depend on
  what happens to be loaded, and a rule silently disappearing is worse than one added by hand.
- **Thresholds are documented with their reasoning** in `docs/validation-rules.md` so they can be argued
  with rather than guessed at.

## Phase 3 — CLI Foundation ✅ complete

- [x] Root command (`SkillForgeCommandLine`) with a working `validate` subcommand
- [x] Global options, recursive so they work before or after the subcommand
- [x] DI bootstrap (`CompositionRoot`) with `ValidateOnBuild`
- [x] Global exception handler in `Program`
- [x] Exit code mapping: 0 clean, 1 validation failure, 2 usage error, 3 unexpected
- [x] Console renderer (`ConsoleReportRenderer`, Spectre.Console)
- [x] `--verbose`, `--quiet`, `--no-color`, plus the `NO_COLOR` environment convention
- [x] Help text, asserted to exist for every command and option
- [x] CLI smoke tests — 299 tests overall
- [-] Logging — `Microsoft.Extensions.Logging` is registered but nothing logs yet. Deferred until a
  command has something worth logging; `--verbose` currently affects report detail, not log level.

### Verified against the built executable

```text
skillforge validate samples/valid-skill          -> 0, VALID
skillforge validate samples/broken-references    -> 1, 2 errors + 1 warning
skillforge validate samples/invalid-frontmatter  -> 1, SF0003 with a line number
skillforge validate --stict                      -> 2, "Unrecognized option"
skillforge --help / --version                    -> 0
skillforge (no arguments)                        -> 2
```

### Decisions taken in this phase

- **A skill that cannot be loaded still gets a report**, in the same shape as any other failure rather
  than a bare error line. Loader diagnostics are merged into the report so a duplicated frontmatter field
  is not lost.
- **Global options are recursive.** Without it they are accepted only before the subcommand, which is not
  how anyone types a command line.
- **An unrecognised option is a usage error, not a path.** `Argument<string>` would otherwise swallow
  `--stict` as the directory to validate. A validator rejects values starting with `-`.
- **Colour is never the only signal.** Every finding carries a text marker (`x`, `!`, `i`) so output reads
  correctly in a log, and `NO_COLOR` is honoured without a flag.
- **`ValidateOnBuild` is on**, so a missing registration fails at startup and in tests instead of on the
  user's first run — see the bug below.
- **`FluentValidation` was dropped from the plan.** Rules are plain classes behind an interface; the
  library would add indirection without removing code. Recorded in `docs/architecture.md`.

### Bug this phase produced, and what it changed

The CLI threw `InvalidOperationException` on every run — the DI container will only use a **public**
constructor, even for an internal type — while the whole suite stayed green. The smoke test named
"resolves every dependency the commands need" only built the command tree, which resolves nothing. It now
resolves the runner for real, and `ValidateOnBuild` makes the container refuse to start in that
situation.

## Phase 4 — `init` ✅ complete

- [x] Template model
- [x] Directory creation
- [x] Frontmatter generation
- [x] `skillforge.yaml` generation
- [x] `--force`
- [x] Invalid name check
- [x] Existing directory check
- [x] Automatic validation after init

## Phase 5 — `validate` ✅ complete

- [x] Console output
- [x] JSON output
- [x] `--output` file
- [x] Strict mode wiring
- [x] Exit codes
- [x] Summary
- [x] Diagnostic ordering
- [x] CI friendly output

## Phase 6 — `inspect` ✅ complete

- [x] File inventory
- [x] External URL extraction
- [x] Script detection
- [x] Permission inference
- [x] Risk indicator summary
- [x] JSON output
- [x] Human-readable output

## Phase 7 — `pack` ✅ complete

- [x] Version resolution
- [x] Include/exclude handling
- [x] Deterministic ZIP
- [x] SHA-256 hash
- [x] Manifest JSON
- [x] Validation gate and `--skip-validation`
- [x] Output directory handling
- [x] Cross-platform path support

## Phase 8 — SARIF and GitHub Action ✅ complete

- [x] SARIF 2.1.0 generator
- [x] Rule metadata
- [x] Location mapping
- [x] GitHub annotation compatibility
- [x] Example workflow
- [x] CI documentation

---

---

## Milestone v0.1.0 — Local Validator: done

All eight phases are complete. 373 tests; `dotnet format --verify-no-changes` clean; CI green on Linux and
Windows, and CI now runs the built CLI over the sample skills including asserting that the broken sample
exits 1.

Verified against the built executable:

```text
init demo-skill                          -> 0, then validates clean (the template's own goal)
inspect demo-skill                       -> 0, files + capabilities + URLs
pack demo-skill                          -> 0, zip + .sha256 + manifest
pack twice                               -> identical sha256
pack samples/broken-references           -> 1, refused; --skip-validation is explicit
validate --format json | --format sarif   -> schema 1.0 / SARIF 2.1.0
```

### Decisions taken in phases 4 to 8

- **`init` generates a skill that passes `validate` with no findings**, including a placeholder description
  that states an activation context. A template that immediately warns would train people to ignore
  warnings. There is a test on the placeholder text for that reason.
- **`SkillName` is the single definition of a valid name**, shared by `init` and SF0006, so `init` cannot
  generate something `validate` rejects.
- **Refusing to overwrite is the runner's job, not the initializer's.** The check happens before anything is
  written, and an existing skill without `--force` is a usage error (exit 2), not a finding about a skill.
- **Machine-readable output does not silence the console.** With `--output`, the file is for machines and the
  console still shows the human-readable report, because a CI log that says nothing is useless.
- **`inspect` describes, never judges.** Everything it reports is informational, it exits 0 even when it lists
  scripts and URLs, and the output says outright that it is not a security verdict (ADR-006).
- **`inspect` reads URLs from `SKILL.md` only**, and says so in the output. Scanning every referenced file is
  a fuller answer and belongs to the security-signals milestone; the limit is stated rather than implied away.
- **Determinism is enforced at the archive level**: entries sorted, timestamps pinned to 1980-01-01, paths
  always `/`. The one field that legitimately varies between builds — creation time — lives in the manifest,
  not in the archive, so the hash stays reproducible.
- **`pack` writes a `.sha256` in `sha256sum -c` format**, so verification needs no SkillForge.
- **A version that cannot be a file name is refused**, rather than producing an archive with a mangled name.
- **SARIF locations are repository-relative.** An absolute agent path matches no file GitHub knows about, so
  the annotation would silently never appear.
- **SF1004 to SF1008 stay planned, with the reason recorded** in `docs/validation-rules.md`. Each needs a
  decision this release does not make — what "unused" means, whether a URL is a problem, what a skill's
  dependencies are.

---

## Code-standards refactor pass (after v0.1.0 feature-complete)

The whole codebase was scanned against a code-review rubric (one-type-per-file, async signatures,
performance, magic values, dead code, test quality) and the findings applied. No critical findings. Package
hash before and after the refactor is identical, which is the evidence that behaviour was preserved.

Applied:

- [x] **One type per file** — 13 source files and 5 test files split; 22 new files. Every request/options
  record, helper static class and secondary interface now lives on its own.
- [x] **`CancellationToken cancellationToken = default`** on every Task/ValueTask-returning declaration
  and its implementations (~30 signatures).
- [x] **Cancellation is checked between rules by the validator**, not repeated inside every synchronous
  rule. Nine of the eleven rules took a token they never used; the check now lives where the loop is, and
  `ISkillValidationRule`'s docs say so.
- [x] **`RuleResult.None()` / `RuleResult.One(...)`** removes the repeated
  `ValueTask.FromResult<IReadOnlyList<Diagnostic>>` plumbing from nine rules, leaving each rule's condition
  and message as the only thing on screen. No base class — the rules stay independent.
- [x] **O(n·m) fixed in `SarifReportSerializer`** — it re-scanned the whole diagnostic list once per code;
  now one lookup is built first.
- [x] **Magic values → constants**: default version, archive/hash/manifest suffixes, default path, default
  licence, default output directory. `inspect` now shares `SkillForgeTool.ReportSchemaVersion` instead of
  repeating `"1.0"`, so the two cannot drift.
- [x] **Dead code removed**: an unused private `ToText` in `SkillPackager`, unused members in both test
  fakes.
- [x] **Synchronous `Console` writes inside `async` methods** replaced with their awaited equivalents.
- [x] **`SkillLoader.LoadAsync` split** — the read-and-split step is now a named helper.
- [x] **Five placeholder `BootstrapTests` deleted** — they asserted `true.Should().BeTrue()` and every
  project has real tests now.
- [x] **The real gap: `init`, `inspect` and `pack` runners had no tests at all**, while exit codes are the
  CLI's entire contract with CI. All three now have them — 399 tests, up from 373.
- [x] Direct tests for `SkillName`, the shared definition behind both `init` and SF0006.

Reported, deliberately not changed:

- **Diagnostic messages end with a full stop.** The rubric says error messages must not. These are
  user-facing sentences in a report, not log lines — stripping the punctuation would make the console
  output read worse. Kept, with the reasoning recorded here rather than silently ignored.
- **The two link-walking rules keep their own loops.** They dedupe on different keys (normalised path vs
  raw target); a shared abstraction over twenty lines each would cost more clarity than it saves.
- **Command runners write to `Console` directly**, so asserting on stdout needs process-global
  `Console.SetOut` — which is exactly what made one new test fail under xUnit's parallel classes. The test
  now asserts the file content instead. The underlying fix is to inject a `TextWriter` into the runners;
  worth doing when a command next needs to change its output, not as part of a standards pass.
- **Two `FakeFileSystem` implementations remain.** The Application one simulates links and read failures;
  the CLI one only needs file existence and content. Sharing them would import unused surface into the CLI
  tests; the dead members were removed so "the smaller fake does less" is now actually true.

---

## Backlog after v0.1.0 — from the ecosystem input of 2026-07-27

Source: `agent-skills-mcp-ekosistem-ozeti.txt`, recorded in roadmap §30. **The external claims in it are
secondhand and unverified here** — useful for direction, not as justification for an investment or a rule
severity until checked against primary sources.

Product position sharpened: providers install skills, SkillForge validates before installation, shows the
behaviour surface, reports what changed and tests compatibility. Not another installer. Not a catalogue yet.

### v0.2 — Security signals and CI

- [x] **`skillforge diff <before> <after>`** — the highest-value item, and absent from the original roadmap.
  Reports *behaviour surface* change rather than file change: permissions added, external domains added,
  scripts added, activation scope broadened, eval results. Architecture hook: it compares two
  `SkillDefinition`s over the surface `inspect` already computes, so what is missing is loading two revisions
  (git or two directories) and modelling the surface delta — not a new reading layer.
  Built as two paths rather than a git range: taking a revision range needs SkillForge to run `git`, a capability
  and a set of failure modes it does not have. `git worktree add` produces the two paths and is documented in
  `docs/ci.md`; built-in support would do the same underneath.
  It refuses to claim an activation scope "broadened" — that cannot be judged honestly from a description's text,
  so the change is shown in full for a human to judge. Testing activation is what v0.3's evals are for.
- [x] Activation-risk rules (`SF3xxx` band) — SF3001 and SF3002. SF3002 read the body too until it was measured:
  16 findings on 229 real skills, roughly one genuine. Body scanning dropped (it belongs to `SF4xxx`) and the
  weakest pattern deleted; the same 229 skills now give 5 findings, all defensible.
- [x] Permission inference, cross-checked against `skillforge.yaml` declarations (SF1006, SF1007)
- [x] External URL and script analysis (SF1005, and the shell patterns in roadmap §11) — seven literal shell
  patterns, each carrying its own reason, and 42 known-positive/negative tests
- [x] GitHub Action, published — composite `action.yml` at the repository root, dogfooded by
  `.github/workflows/action.yml` on both of its outcomes
- [x] PR annotations — via SARIF upload to code scanning, which is what puts findings inline on the pull request
- [x] Publish `SkillForge.Cli` to NuGet.org — `.github/workflows/release.yml`, triggered by a `v*` tag, using
  trusted publishing so no API key exists anywhere. Three things are pinned by the nuget.org policy and break
  publishing if changed without updating it: the workflow file name, the `production` environment, and the
  account username in the `NUGET_USER` secret.
- [-] PR annotations carrying the *diff* summary — deferred: `diff` has no SARIF output yet, and inventing one
  to carry a summary that is not a finding would misuse the format
- [x] Rule suppression / configurable validation — `--suppress` plus a real `validation` section in
  `skillforge.yaml`. Measured on 229 skills: 610 warnings become 147 with `Suppressed: 463` shown, so `--strict`
  is finally usable on an existing collection. Suppression is unrestricted (errors included) because a repository
  that has decided a rule does not apply has a reason we cannot see — the always-reported count is what keeps it
  honest.

### Found while building SF6001

- [x] A top-level `version:` in frontmatter is silently ignored — the schema reads it from `metadata.version`, so
  an author who writes it at the top level gets no version anywhere: no SF0010 check, nothing in `inspect`,
  `pack` or `diff`, and SF6001 cannot fire. Found by writing a fixture the wrong way and believing the tool.
  Deciding whether to accept both spellings, or to report the misplaced one, is a schema decision rather than a
  bug fix, so it is a task rather than a patch.

### v0.3 — Evals and provider compatibility

- [ ] `skillforge eval` with deterministic assertions
- [ ] Positive and negative activation tests: does the skill fire when it should, and stay quiet when it should not
- [ ] Provider compatibility checks
- [ ] Codex / Claude Code / Copilot adapters

### v0.4 — Migration and MCP

- [ ] `skillforge migrate inspect` — read Cursor / Claude Code / Codex / Copilot configuration and report the
  skill inventory, MCP inventory, conflicting instructions, missing dependencies and provider incompatibilities
- [ ] MCP protocol inspection, behind version adapters rather than in the CLI core: protocol version in use,
  deprecated capability use, stateful transport dependency, authorization method, tool schema conformance
- [ ] MCP 2025-11-25 and 2026-07-28 adapters
- [ ] Deprecated capability detection

### New diagnostic bands — and the stance this changes

| Band | Scope | Status |
|---|---|---|
| `SF3xxx` | Activation and retrieval risks | SF3001, SF3002 shipped |
| `SF4xxx` | Instruction injection risks | SF4001, SF4002 shipped |
| `SF5xxx` | Supply-chain and provenance risks | SF5001 shipped; provenance deferred |
| `SF6xxx` | Version and evolution risks | SF6001 shipped (`diff`); "no version declared" deferred at 91% firing |

Through v0.1.0 the code set was deliberately fixed at 24 — an unreadable `SKILL.md` widened SF0001's meaning
rather than inventing a 25th code. These bands lift that constraint **on purpose**: the code set is open,
while the *meaning and severity of a published code* stays fixed. Adding a code is fine; redefining one is not.

**A rule is measured before it is published.** SF1009 fires on 30 of 32 real skills and SF1010 on 32 of 32;
SF0008 calls a sibling-skill reference an error. Those were found by running the tool on real input, not by
reading the rule. Every new rule in these bands gets the same treatment first — a rule that fires on almost
every input is noise, and noise teaches people to ignore warnings.

### Sandbox scanning, when it arrives

Not built on "it ran in a container, so it is safe". Agents have been shown to reach the host's IDE, Git and
extension components through repository content. So a sandbox run also watches: repository diff before and
after, Git config changes, IDE/agent config changes, hook creation, symlink creation, writes outside the
workspace, and files that persist to affect the next run.

## Out of scope for v0.1.0

Web panel · public marketplace · private registry · user and organisation management · Auth0 ·
payments · model-based evals · Docker sandbox · remote package repository · MCP server management ·
central policy engine · telemetry · Kubernetes · microservices · database.
