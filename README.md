# SkillForge

A local, open source CLI for AI agent skills. SkillForge creates, validates, inspects and packages
`SKILL.md`-based skills, and reports findings as human-readable console output, JSON or SARIF.

> Status: **pre-alpha**. Phase 0 (repository bootstrap) is complete; no CLI commands are implemented yet.
> CI builds and tests on Linux and Windows.
> See [SKILLFORGE_ROADMAP.md](SKILLFORGE_ROADMAP.md) for scope and [TODO.md](TODO.md) for progress.

## Why

Agent skills are executable instructions that ship with real permissions. SkillForge lets a developer
notice a broken or risky skill in seconds — locally, and in CI — without sending anything to a service.

SkillForge reports concrete diagnostics and risk signals. It deliberately does **not** label a skill
"safe" or "unsafe".

## Planned commands

| Command | Purpose |
|---|---|
| `skillforge init <name>` | Scaffold a new skill folder |
| `skillforge validate <path>` | Validate structure, frontmatter and quality rules |
| `skillforge inspect <path>` | Summarise files, links, scripts and inferred permissions |
| `skillforge pack <path>` | Produce a deterministic `.skill.zip` with a SHA-256 hash and manifest |

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
dotnet test --collect:"XPlat Code Coverage"
```

Run the CLI from source:

```bash
dotnet run --project src/SkillForge.Cli -- --help
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
