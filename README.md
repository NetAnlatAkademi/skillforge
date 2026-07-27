<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo.svg" alt="SkillForge" width="296" height="72">
  </picture>
</p>

# SkillForge

A local, open source CLI for AI agent skills. SkillForge creates, validates, inspects and packages
`SKILL.md`-based skills, and reports findings as human-readable console output, JSON or SARIF.

> Status: **v0.1.0 in development.** All four commands work end to end. CI builds and tests on Linux and
> Windows, and runs the CLI over the sample skills.

## Try it

```bash
dotnet run --project src/SkillForge.Cli -- validate ./samples/valid-skill
dotnet run --project src/SkillForge.Cli -- validate ./samples/broken-references --verbose
```

```text
SkillForge Validate

Skill: broken-references
Path:  ./samples/broken-references

x SF0007 The referenced file 'references/checklist.md' does not exist in the skill. (SKILL.md:16)
x SF0007 The referenced file 'scripts/analyze.ps1' does not exist in the skill. (SKILL.md:17)
! SF1010 No agent compatibility is declared. (SKILL.md:1)

Result: INVALID
Errors: 2  Warnings: 1  Info: 0
```

Exit codes: `0` clean · `1` validation failure, or a warning under `--strict` · `2` usage error ·
`3` unexpected failure. Options: `--strict`, `--quiet`, `--verbose`, `--no-color` (the `NO_COLOR`
environment variable works too).
> See [SKILLFORGE_ROADMAP.md](SKILLFORGE_ROADMAP.md) for scope and [TODO.md](TODO.md) for progress.

## Why

Agent skills are executable instructions that ship with real permissions. SkillForge lets a developer
notice a broken or risky skill in seconds — locally, and in CI — without sending anything to a service.

SkillForge reports concrete diagnostics and risk signals. It deliberately does **not** label a skill
"safe" or "unsafe".

## Commands

| Command | Purpose |
|---|---|
| `skillforge init <name>` | Scaffold a skill that already passes validation |
| `skillforge validate <path>` | Validate structure, frontmatter and quality rules |
| `skillforge inspect <path>` | Summarise files, links, scripts and inferred capabilities |
| `skillforge pack <path>` | Produce a deterministic `.skill.zip` with a SHA-256 hash and manifest |

Full options are in [docs/cli-reference.md](docs/cli-reference.md); CI usage, including SARIF upload, is in
[docs/ci.md](docs/ci.md).

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer

## Build and test

```bash
dotnet restore
dotnet build
dotnet test
```

With coverage:

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

The run settings exclude source-generated code. Without them, the matchers emitted by
`[GeneratedRegex]` dominate the numbers and hide real gaps in hand-written code.

Run the CLI from source:

```bash
dotnet run --project src/SkillForge.Cli -- --help
```

## Install as a global tool

Once published:

```bash
dotnet tool install --global SkillForge.Cli
skillforge --help
```

Or build the package locally:

```bash
dotnet pack src/SkillForge.Cli -c Release
dotnet tool install --global --add-source src/SkillForge.Cli/bin/Release SkillForge.Cli
```

## Repository layout

```text
skillforge/
├── docs/         Architecture, validation rules, CLI reference
├── samples/      Example skills used by integration tests
├── src/          Domain, Application, Infrastructure, Reporting, Cli
└── tests/        One xUnit project per source project
```

Layer responsibilities and dependency rules are described in [docs/architecture.md](docs/architecture.md).

## Contributing

- Nullable reference types and warnings-as-errors are enabled; the build must stay clean.
- Every new NuGet package needs a written justification in `docs/architecture.md`.
- Validation rules live in the Application layer, never in a command class.
- Commits follow Conventional Commits, e.g. `feat(validation): validate skill frontmatter`.

## License

[MIT](LICENSE)
