# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
