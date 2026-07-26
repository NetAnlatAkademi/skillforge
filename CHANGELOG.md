# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
  with a line number instead of an exception.
- Four sample skills under `samples/` used as test fixtures: `valid-skill`, `invalid-frontmatter`,
  `broken-references` and `dotnet-api-review`.
