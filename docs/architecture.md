# Architecture

## Shape

SkillForge is a **modular monolith** with a simplified Clean Architecture layering. There is no
database, no service boundary and no container requirement.

```text
SkillForge.Cli            Commands, options, exit codes, DI bootstrap
        │
        ├──────────────► SkillForge.Reporting        Console / JSON / SARIF renderers
        │                        │
        └──────────────► SkillForge.Infrastructure   File system, YAML, ZIP, SHA-256
                                 │
                        SkillForge.Application       Use cases, validation orchestration
                                 │
                        SkillForge.Domain            Models, diagnostics, severities
```

## Dependency rules

| Layer | May reference | Must not reference |
|---|---|---|
| `Domain` | nothing | any other project, any third-party framework |
| `Application` | `Domain` | `Infrastructure`, `Reporting`, `Cli`, `System.IO` for real I/O |
| `Infrastructure` | `Application`, `Domain` | `Reporting`, `Cli` |
| `Reporting` | `Application`, `Domain` | `Infrastructure`, `Cli` |
| `Cli` | all of the above | — |

Consequences:

- The Application layer reaches the file system only through abstractions it owns, so every rule is
  unit-testable without touching disk.
- Command classes contain no business logic. They parse input, call a use case and map the result to
  an exit code.
- Expected validation failures are returned as a result model, not thrown. Exceptions are reserved for
  genuinely unexpected states.

## Cross-cutting conventions

- **Nullability:** enabled everywhere; `TreatWarningsAsErrors` keeps it honest.
- **Async:** all I/O is async, methods end in `Async`, and every public async method accepts a
  `CancellationToken`.
- **Time:** UTC only, obtained through `TimeProvider` so tests can control it. `DateTime.UtcNow` is not
  called directly in production code.
- **Paths:** normalised before use. No hard-coded OS separators. Access outside the skill root is
  rejected, including via symlinks and `..` segments.
- **Diagnostics:** every rule owns a stable code (`SF0001`, `SF1001`, `SF2001`, …). Codes are never
  reused or renumbered once released.
- **Determinism:** diagnostic ordering and package contents are deterministic, so identical input
  produces an identical hash.

## Versioning

`YY.DayOfYear.Build` — `26.208.1` is the first build on 27 July 2026. The date parts are computed at build time
in UTC; the counter comes from `SKILLFORGE_BUILD` and is `1` locally.

Two things follow from this, and both are deliberate:

- **No zero padding.** SemVer forbids leading zeroes in numeric identifiers, so `26.208.01` is not a valid version
  and NuGet normalises it away. Padding the git tag but not the package would give one release two spellings.
- **Two builds of the same commit on different days carry different versions.** That is what a dated scheme means.
  It shows up as `tool.version` in JSON and SARIF, which is correct — a report should say which build produced it —
  and it never affects a package hash, which covers the skill's files and not SkillForge's own version.

**Milestone names are not version numbers.** "v0.1.0 — Local Validator" and "v0.2.0 — Security Signals" in the
roadmap are labels for *scope*; releases are dated. A release note says which milestone's work it contains.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success, no errors |
| 1 | Validation error (or a warning under `--strict`) |
| 2 | Invalid CLI usage |
| 3 | Unexpected application failure |

## Package inventory and justification

Every NuGet dependency is declared centrally in `Directory.Packages.props`. New entries require a
justification row here.

| Package | Layer | Justification |
|---|---|---|
| `System.CommandLine` | Cli | Argument parsing, help generation and completions. The Microsoft-owned option; hand-rolling a parser would mean reimplementing help text and error reporting badly. |
| `Spectre.Console` | Reporting | Console rendering with colour that can be switched off. Chosen over raw `Console` for markup escaping and testable output via `IAnsiConsole`. |
| `Microsoft.Extensions.DependencyInjection` | Cli | Composition root. Small enough to hand-wire today, but the rule set already benefits from `GetServices<T>()`. |
| `Microsoft.Extensions.Logging`(`.Console`) | Cli | Diagnostic logging behind `--verbose`. Abstractions only in the lower layers. |
| `YamlDotNet` | Infrastructure | YAML frontmatter parsing. The de-facto .NET YAML library; writing a YAML parser by hand is out of the question, and the alternatives are unmaintained. Confined to Infrastructure so Application stays parser-agnostic behind `IFrontmatterParser`. |
| `Microsoft.NET.Test.Sdk` | tests | Required test host for `dotnet test`. |
| `xunit`, `xunit.runner.visualstudio` | tests | Test framework chosen by the roadmap. |
| `FluentAssertions` 7.x | tests | Readable assertions. Pinned to 7.x because 8.x moved to a commercial license. |
| `coverlet.collector` | tests | Coverage collection for the roadmap's coverage targets. |

`FluentValidation` is named by the roadmap but has not been needed: validation rules are plain classes
behind `ISkillValidationRule`, and a rule library would add indirection without removing any code.

## Testing strategy

One test project per source project. Coverage targets: Domain 90%+, Application 80%+,
Infrastructure 70%+, plus mandatory CLI smoke tests. Coverage alone is not the bar — every validation
rule must have a test that asserts its diagnostic code.
