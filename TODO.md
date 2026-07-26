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

### Deviations from the roadmap

- The solution file is `SkillForge.slnx`, not `SkillForge.sln`. The .NET 10 SDK produces the XML
  solution format by default; it is supported by `dotnet` and Visual Studio 17.14+.
- `NuGet.config` was added. Not listed in the roadmap, but CPM fails restore when the machine defines
  more than one unmapped package source.
- Only test packages are declared in CPM. `System.CommandLine`, `YamlDotNet`, `FluentValidation`,
  `Spectre.Console` and `Microsoft.Extensions.*` are added in the phase that first needs them, so
  Phase 0 ships no unused dependencies.

---

## Phase 1 — Skill Loader

- [ ] Domain models: `SkillDefinition`, `SkillFrontmatter`, `SkillResource`, `Diagnostic`, `DiagnosticSeverity`
- [ ] `OperationResult<T>` result model
- [ ] Diagnostic code constants (`SF0001`…`SF2004`)
- [ ] `IFileSystem` abstraction in Application, implementation in Infrastructure
- [ ] Skill root detection
- [ ] `SKILL.md` discovery
- [ ] Frontmatter / body separation
- [ ] YAML parsing (introduces `YamlDotNet` — justify in `docs/architecture.md`)
- [ ] Markdown body reading
- [ ] Resource file enumeration
- [ ] Path normalisation
- [ ] Symlink handling
- [ ] Loader diagnostics (malformed YAML must not crash)
- [ ] Sample skills: `valid-skill`, `invalid-frontmatter`, `broken-references`, `dotnet-api-review`
- [ ] Unit and integration tests
- [ ] Update `docs/validation-rules.md` and `CHANGELOG.md`

## Phase 2 — Validation Engine

- [ ] `ISkillValidationRule` interface
- [ ] Rule discovery and execution pipeline (one failing rule must not stop the others)
- [ ] Required field rules (SF0004, SF0005)
- [ ] Name format rules (SF0006)
- [ ] Description quality rules (SF1001, SF1002)
- [ ] File reference rules (SF0007)
- [ ] Path traversal rules (SF0008)
- [ ] Length rules (SF1003)
- [ ] License and compatibility warnings (SF1009, SF1010)
- [ ] Summary calculation
- [ ] Strict mode
- [ ] Deterministic diagnostic ordering

## Phase 3 — CLI Foundation

- [ ] Root command and global options (introduces `System.CommandLine`)
- [ ] DI bootstrap and logging
- [ ] Global exception handler
- [ ] Exit code mapping (0/1/2/3)
- [ ] Console renderer (introduces `Spectre.Console`)
- [ ] `--verbose`, `--quiet`, `--no-color`
- [ ] Help text
- [ ] CLI smoke tests

## Phase 4 — `init`

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
