# Validation rules

Every rule owns a stable diagnostic code. Codes are never reused or renumbered once released.

- `SF0xxx` — **Error**. The skill is not usable as written.
- `SF1xxx` — **Warning**. The skill works but quality or risk deserves attention.
- `SF2xxx` — **Info**. A neutral observation about the skill's surface.

The `Status` column tracks implementation, so the table doubles as a checklist. `Planned` means the
code is reserved but no rule exists yet.

## Errors

| Code | Rule | Status |
|---|---|---|
| SF0001 | `SKILL.md` was not found | Planned |
| SF0002 | YAML frontmatter was not found | Planned |
| SF0003 | YAML frontmatter could not be parsed | Planned |
| SF0004 | `name` field is missing | Planned |
| SF0005 | `description` field is missing | Planned |
| SF0006 | Skill name is invalid | Planned |
| SF0007 | A referenced file was not found | Planned |
| SF0008 | A path escapes the skill directory | Planned |
| SF0009 | The same metadata field is declared more than once | Planned |
| SF0010 | Package version is invalid | Planned |

## Warnings

| Code | Rule | Status |
|---|---|---|
| SF1001 | Description is too short | Planned |
| SF1002 | Description does not state an activation context | Planned |
| SF1003 | `SKILL.md` is longer than 500 lines | Planned |
| SF1004 | An unused file is present | Planned |
| SF1005 | An external URL is present | Planned |
| SF1006 | A script file exists but no permission is declared | Planned |
| SF1007 | A shell command requests broad privileges | Planned |
| SF1008 | Package dependencies are not pinned | Planned |
| SF1009 | No license is declared | Planned |
| SF1010 | No agent compatibility information is declared | Planned |

## Info

| Code | Rule | Status |
|---|---|---|
| SF2001 | The skill contains a script | Planned |
| SF2002 | The skill contains an external URL | Planned |
| SF2003 | The skill contains a binary file | Planned |
| SF2004 | The skill contains an `evals` folder | Planned |

## Security signals

Phase 2 and Milestone v0.2.0 detect the patterns listed in the roadmap (piped shell installers,
`rm -rf`, `Invoke-Expression`, `chmod 777`, `sudo`, privileged containers; sensitive paths such as
`.env`, `.ssh`, `/etc/`; network calls; secret-shaped identifiers).

These checks only ever produce diagnostics. SkillForge does not classify a skill as safe or malicious
(ADR-006).
