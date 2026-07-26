# SkillForge — TODO

Single source of truth for progress. Derived from [SKILLFORGE_ROADMAP.md](SKILLFORGE_ROADMAP.md);
mirrored into the Obsidian vault under `SkillForge/` for cross-session context.

Legend: `[ ]` open · `[x]` done · `[~]` in progress · `[-]` deliberately deferred

Last updated: 2026-07-26

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
- [-] Make the repository public — deferred until v0.1.0 is usable

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

## Phase 4 — `init` (next)

- [ ] Template model
- [ ] Directory creation
- [ ] Frontmatter generation
- [ ] `skillforge.yaml` generation
- [ ] `--force`
- [ ] Invalid name check
- [ ] Existing directory check
- [ ] Automatic validation after init

## Phase 5 — `validate`

- [ ] Console output
- [ ] JSON output
- [ ] `--output` file
- [ ] Strict mode wiring
- [ ] Exit codes
- [ ] Summary
- [ ] Diagnostic ordering
- [ ] CI friendly output

## Phase 6 — `inspect`

- [ ] File inventory
- [ ] External URL extraction
- [ ] Script detection
- [ ] Permission inference
- [ ] Risk indicator summary
- [ ] JSON output
- [ ] Human-readable output

## Phase 7 — `pack`

- [ ] Version resolution
- [ ] Include/exclude handling
- [ ] Deterministic ZIP
- [ ] SHA-256 hash
- [ ] Manifest JSON
- [ ] Validation gate and `--skip-validation`
- [ ] Output directory handling
- [ ] Cross-platform path support

## Phase 8 — SARIF and GitHub Action

- [ ] SARIF 2.1.0 generator
- [ ] Rule metadata
- [ ] Location mapping
- [ ] GitHub annotation compatibility
- [ ] Example workflow
- [ ] CI documentation

---

## Out of scope for v0.1.0

Web panel · public marketplace · private registry · user and organisation management · Auth0 ·
payments · model-based evals · Docker sandbox · remote package repository · MCP server management ·
central policy engine · telemetry · Kubernetes · microservices · database.
