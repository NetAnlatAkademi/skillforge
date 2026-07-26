# CLI reference

```text
skillforge [global options] <command> [arguments] [options]
```

Global options work before or after the command:

| Option | Effect |
|---|---|
| `--quiet`, `-q` | Print only errors and the final verdict |
| `--verbose` | Print the suggestion attached to each finding |
| `--no-color` | Disable colour. The `NO_COLOR` environment variable does the same |
| `--help`, `-h` | Show help |
| `--version` | Show the tool version |

Exit codes are documented in [ci.md](ci.md).

---

## `skillforge init`

Creates a new skill from a template. The generated skill passes `validate` with no findings — that is
asserted by a test, so a template change cannot quietly start producing warnings.

```bash
skillforge init my-skill
skillforge init my-skill --description "Reviews ASP.NET Core APIs" --author "Çağrı" --license MIT
skillforge init my-skill --directory ./skills/my-skill
```

| Option | Default | Effect |
|---|---|---|
| `--directory`, `-d` | a directory named after the skill | Where to create it |
| `--description` | a placeholder that states an activation context | Frontmatter description |
| `--author` | omitted | Recorded under `metadata.author` |
| `--license` | `MIT` | SPDX identifier |
| `--force` | off | Overwrite an existing skill |

Creates `SKILL.md`, `skillforge.yaml`, and `references/`, `scripts/`, `assets/`, `evals/` — each with a
README, because git does not track empty directories.

Without `--force`, a directory that already contains a `SKILL.md` is refused with exit code 2. The check
happens before anything is written.

---

## `skillforge validate`

Validates a skill against the rules in [validation-rules.md](validation-rules.md).

```bash
skillforge validate ./my-skill
skillforge validate ./my-skill --strict
skillforge validate ./my-skill --format json --output artifacts/report.json
skillforge validate ./my-skill --format sarif --output artifacts/skillforge.sarif
```

| Option | Default | Effect |
|---|---|---|
| `path` | `.` | Skill directory, or a `SKILL.md` path |
| `--strict` | off | Treat warnings as failures |
| `--format`, `-f` | `console` | `console`, `json` or `sarif` |
| `--output`, `-o` | stdout | Write machine-readable output to a file |

When `--output` is used, the human-readable report still goes to the console: a CI log that says nothing
about why a build failed is not much use.

A skill that cannot be loaded at all still produces a report, in the same shape as any other failure.

---

## `skillforge inspect`

Summarises what a skill contains and what its contents imply it can do.

```bash
skillforge inspect ./my-skill
skillforge inspect ./my-skill --format json
skillforge inspect ./my-skill --show-permissions
```

| Option | Default | Effect |
|---|---|---|
| `path` | `.` | Skill directory, or a `SKILL.md` path |
| `--format`, `-f` | `console` | `console` or `json` |
| `--output`, `-o` | stdout | Write to a file |
| `--show-files` | all sections | Only the file inventory |
| `--show-links` | all sections | Only external URLs |
| `--show-permissions` | all sections | Only inferred capabilities and declared tools |

Inspect describes; it does not judge. A skill that ships a script and three URLs still exits 0. Capabilities
are inferred from the contents (`Filesystem Read`, `Shell Execution`, `Network Access`, `Binary Content`) and
the output says outright that it is not a security verdict.

URLs are read from `SKILL.md` only. Scanning every referenced file is a fuller answer and belongs to the
security-signals milestone; until then the limitation is stated rather than implied away.

---

## `skillforge pack`

Packages a skill into a deterministic archive with a hash and a manifest.

```bash
skillforge pack ./my-skill
skillforge pack ./my-skill --output ./dist
skillforge pack ./my-skill --version-override 1.0.0
```

| Option | Default | Effect |
|---|---|---|
| `path` | `.` | Skill directory, or a `SKILL.md` path |
| `--output`, `-o` | `artifacts` | Directory to write to |
| `--version-override` | the skill's `metadata.version` | Version to package as |
| `--skip-validation` | off | Package even when validation finds errors |

Produces three files:

```text
artifacts/
├── my-skill.1.0.0.skill.zip
├── my-skill.1.0.0.skill.zip.sha256
└── my-skill.1.0.0.manifest.json
```

The archive is deterministic — entries sorted, timestamps pinned, paths always using `/` — so the same
contents produce the same hash on any machine. The `.sha256` file uses the format `sha256sum -c` expects.

Validation is a gate. A skill with errors is not packaged unless `--skip-validation` is passed, and when it
is, the CLI says so rather than doing it silently. Tooling directories (`bin`, `obj`, `.git`, `node_modules`,
`artifacts`, …) are never included.
