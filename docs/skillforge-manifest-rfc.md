# RFC: `skillforge.yaml`

**Status:** draft, implemented as read-optional. **Schema version:** 1.

## Why a second file

`SKILL.md` is a shared standard: several agents read it, and SkillForge does not get to change it (ADR-003).
Anything SkillForge-specific therefore lives in its own file, so a skill written for SkillForge stays a
perfectly ordinary skill for anything else.

The file is **optional**. A skill without one validates and packages exactly the same, using the defaults
below, and its absence is not a warning — requiring it would make SkillForge's own conventions a condition of
being a valid skill, which is the thing this split exists to avoid.

## Shape

```yaml
schemaVersion: 1

package:
  version: 0.1.0
  publisher: local

compatibility:
  agents:
    - claude-code
    - codex
    - github-copilot

permissions:
  filesystem:
    read: []
    write: []
  shell:
    allowed: []
  network:
    allowed: false
  secrets: []

validation:
  strict: false

packageOptions:
  include:
    - "SKILL.md"
    - "references/**"
    - "scripts/**"
    - "assets/**"
    - "evals/**"
  exclude:
    - ".git/**"
    - "bin/**"
    - "obj/**"
    - ".DS_Store"
```

## Defaults when the file is absent

| Setting | Default |
|---|---|
| `package.version` | `metadata.version` from `SKILL.md`, then `0.1.0` |
| `compatibility.agents` | whatever `SKILL.md` declares |
| `permissions` | nothing declared; `inspect` reports inferred capabilities instead |
| `validation.strict` | `false`; `--strict` on the command line decides |
| `packageOptions` | everything in the directory except tooling directories |

## What is implemented today

`init` generates the file, and `pack` applies the equivalent exclusions. The remaining fields are **declared,
not enforced** — a skill can claim `network.allowed: false` and still contain a URL, and SkillForge will
report the URL through `inspect` rather than treating the declaration as a policy it enforces.

That gap is deliberate rather than unfinished: enforcing a permission model means deciding what a violation
is, and this release does not make that decision. Reading and cross-checking these fields — flagging a script
with no declared shell permission, a URL with `network.allowed: false` — is the security-signals milestone
(SF1006, SF1007).

## Open questions

1. **Should `permissions` be a manifest or a request?** A declaration the tool cannot enforce risks reading as
   a guarantee. Naming it `declaredPermissions` would be more honest.
2. **Glob semantics.** `packageOptions` uses glob syntax; which dialect is not yet pinned down. Until it is,
   `pack` uses its built-in exclusions rather than interpreting the field.
3. **Version precedence.** Today `SKILL.md`'s `metadata.version` wins because it is the file the agent reads.
   If both are present and disagree, that arguably deserves a diagnostic of its own.

Anyone changing this file's shape should bump `schemaVersion` and record why here.
