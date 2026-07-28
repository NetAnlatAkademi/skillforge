# SkillForge

A local, open source CLI for AI agent skills. It creates, validates, inspects, compares and packages
`SKILL.md`-based skills, and reports findings as console text, JSON or SARIF 2.1.0.

Nothing is sent to any service. Validation, inspection and packaging all run on your machine.

## Install

```bash
dotnet tool install --global SkillForge.Cli
skillforge --help
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer.

## Commands

| Command | What it does |
|---|---|
| `skillforge init <name>` | Scaffold a skill that already passes validation |
| `skillforge validate <path>` | Validate structure, frontmatter, quality rules and provider compatibility |
| `skillforge inspect <path>` | Summarise files, links, scripts and inferred capabilities |
| `skillforge diff <before> <after>` | Compare two versions by what they can do, not which bytes changed |
| `skillforge eval <path>` | Check a skill against the expectations declared under `evals/` |
| `skillforge pack <path>` | Produce a deterministic `.skill.zip` with a SHA-256 hash and manifest |
| `skillforge migrate inspect` | Report the agent tooling installed here: skills, MCP servers, instruction files |

## Validate one skill, or a whole repository

```bash
skillforge validate ./my-skill
skillforge validate ./skills                  # a directory of skills, as one batch
skillforge validate ./skills --format sarif -o skillforge.sarif
```

```text
SkillForge Validate

Skill: broken-references
Path:  ./samples/broken-references

x SF0007 The referenced file 'references/checklist.md' does not exist in the skill. (SKILL.md:16)
x SF0007 The referenced file 'scripts/analyze.ps1' does not exist in the skill. (SKILL.md:17)
! SF1010 No agent compatibility is declared. (SKILL.md:1)
         fix:  add to the frontmatter:  compatibility: [claude-code]

Result: INVALID
Errors: 2  Warnings: 1  Info: 0
```

A finding whose resolution is one known edit carries the literal text to paste. Most findings have none and stay
silent, because inventing a fix for something only a human can judge is worse than saying nothing.

Rules fire in bands: errors `SF0001`–`SF0010`, quality warnings `SF1001`–`SF1015`, informational `SF2001`–`SF2004`,
activation risks `SF3xxx`, body instruction injection `SF4xxx`, supply chain `SF5xxx`, evolution `SF6xxx`, provider
compatibility `SF7xxx`. Every rule's firing rate was measured on 229 real skills before it shipped, and several
candidate rules were rejected on that measurement — the reasoning is in
[docs/validation-rules.md](https://github.com/NetAnlatAkademi/skillforge/blob/main/docs/validation-rules.md).

Two rules fire on approximately every real skill (`SF1009` no licence, `SF1010` no compatibility), so `--strict` is
documented as unusable by default. Silence them per repository and keep the count visible:

```bash
skillforge validate ./skills --strict --suppress SF1009,SF1010
```

## Check a skill against an agent provider

```bash
skillforge validate ./my-skill --provider claude-code
```

A skill is checked against the providers it declares under `compatibility`, and against nothing else — judging
every skill against every provider would report portability problems to authors who never claimed to be portable.
`claude-code`, `codex`, `cursor` and `github-copilot` are recognised.

## See what changed between two versions

```bash
skillforge diff ./before ./after
```

Reports the behaviour surface: permissions added, external domains added, scripts added, description changed. A
skill whose reach grew while its declared version stayed the same is `SF6001` — a consumer pinned to that version
received the change without being told.

## Package a skill

```bash
skillforge pack ./my-skill --output ./dist
```

Produces `<name>-<version>.skill.zip`, a `.sha256` in `sha256sum -c` format, and a JSON manifest. The archive is
deterministic: entries sorted, timestamps pinned, paths always `/`. Packing the same skill twice gives the same
hash. Validation gates the pack unless you pass `--skip-validation` explicitly.

## What is installed on this machine

```bash
skillforge migrate inspect .
```

Lists which providers are present, what skills each has, what MCP servers each declares and which instruction
files are in play. It describes and never judges, and **environment variable values are never read or printed** —
only the names a declaration sets.

## Ask a model whether it would choose the skill

Everything above runs locally and sends nothing anywhere. One question cannot be answered that way, so `eval` can ask a
model — opt-in, per run, with no default endpoint:

```bash
skillforge eval ./my-skill --model qwen3:8b --model-endpoint http://localhost:11434/v1
```

You pick the model, local or hosted: the transport is OpenAI-compatible, which Ollama, LM Studio, vLLM, OpenRouter and
OpenAI all speak. The API key is passed as the **name** of an environment variable, never a value. The skill competes
against its siblings as distractors over several runs, so the answer is a rate rather than an anecdote, and model results
carry no diagnostic code — they are reported separately from the deterministic findings.

## In CI

There is a GitHub Action that runs `validate`, writes SARIF and uploads it to code scanning, so findings land
inline on the pull request:

```yaml
- uses: NetAnlatAkademi/skillforge@v26.209.3
  with:
    path: ./skills
    suppress: SF1009,SF1010
```

Exit codes: `0` clean · `1` validation failure, or a warning under `--strict` · `2` usage error · `3` unexpected
failure. Options `--strict`, `--quiet`, `--verbose`, `--no-color` apply everywhere, and the `NO_COLOR`
environment variable is honoured without a flag.

## What it deliberately does not do

SkillForge reports concrete diagnostics and risk signals. It never labels a skill "safe" or "unsafe": that is a
judgement about intent, and a tool that claims it teaches people to trust the claim instead of reading the skill.

## Documentation

- [README](https://github.com/NetAnlatAkademi/skillforge#readme)
- [CLI reference](https://github.com/NetAnlatAkademi/skillforge/blob/main/docs/cli-reference.md)
- [Every rule, with its measurements](https://github.com/NetAnlatAkademi/skillforge/blob/main/docs/validation-rules.md)
- [CI and SARIF](https://github.com/NetAnlatAkademi/skillforge/blob/main/docs/ci.md)
- [Migration inventory](https://github.com/NetAnlatAkademi/skillforge/blob/main/docs/migration.md)
- [Changelog](https://github.com/NetAnlatAkademi/skillforge/blob/main/CHANGELOG.md)

MIT licensed.
