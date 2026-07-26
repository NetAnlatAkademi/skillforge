# Validation rules

Every rule owns a stable diagnostic code. Codes are never reused or renumbered once released.

- `SF0xxx` — **Error**. The skill is not usable as written.
- `SF1xxx` — **Warning**. The skill works but quality or risk deserves attention.
- `SF2xxx` — **Info**. A neutral observation about the skill's surface.

The `Status` column tracks implementation, so the table doubles as a checklist. `Planned` means the
code is reserved but no rule exists yet.

Codes marked *(loader)* are produced while reading the skill rather than by a validation rule. The
loader reports only what prevents a skill from being modelled at all; everything else — including a
missing `name` or `description` — is a rule that runs on the loaded model.

When one mistake would trigger two codes, the more precise one wins. A duplicated frontmatter field
also makes the YAML parser fail, so SF0009 is reported and SF0003 is suppressed.

SF0001 covers both "not found" and "found but unreadable" — a locked file or a permission error reads
the same way to the person running the CLI, and inventing a code outside the roadmap's table would
break the fixed set of 24.

Two codes have a defined precedence so one mistake produces one finding:

- SF0004 suppresses SF0006 and SF0007 has nothing to say about it — a skill with no name is not also
  reported as having an invalid one.
- SF0005 suppresses SF1001 and SF1002 for the same reason.
- SF0008 suppresses SF0007 for a given reference: a link pointing outside the skill is an escape, not a
  missing file.

## Thresholds and heuristics

Rules that judge rather than check state their bar here, so a disagreement can be argued with the
number in front of it.

| Rule | Bar | Why |
|---|---|---|
| SF0006 | 2–64 characters, `^[a-z][a-z0-9]*(-[a-z0-9]+)*$` | The name becomes a package file name, a directory name and a command line argument on three operating systems. |
| SF0007 | Case-sensitive comparison on every platform | `References/Notes.md` for `references/notes.md` works on Windows and breaks on Linux. Reporting it everywhere names the portability bug up front. |
| SF0010 | Semantic versioning | Consumers need to be able to compare two versions. A version is optional; only a malformed one is an error. |
| SF1001 | 40 characters | An agent choosing between skills has only the description. "Reviews APIs." does not distinguish this one from ten others. |
| SF1002 | Mentions *when, whenever, while, during, before, after, if* | A deliberate heuristic. A description can state its trigger without those words, which is why this is a warning the author may ignore. |
| SF1003 | 500 lines | A long entry point means reference material should live in its own file that the agent reads only when needed. |

## Errors

| Code | Rule | Status |
|---|---|---|
| SF0001 | `SKILL.md` was not found, or exists but could not be read | **Implemented** (loader) |
| SF0002 | YAML frontmatter was not found | **Implemented** (loader) |
| SF0003 | YAML frontmatter could not be parsed | **Implemented** (loader) |
| SF0004 | `name` field is missing | **Implemented** |
| SF0005 | `description` field is missing | **Implemented** |
| SF0006 | Skill name is invalid | **Implemented** |
| SF0007 | A referenced file was not found | **Implemented** |
| SF0008 | A path escapes the skill directory | **Implemented** (loader + rule) |
| SF0009 | The same metadata field is declared more than once | **Implemented** (loader) |
| SF0010 | Package version is invalid | **Implemented** |

## Warnings

| Code | Rule | Status |
|---|---|---|
| SF1001 | Description is too short | **Implemented** |
| SF1002 | Description does not state an activation context | **Implemented** |
| SF1003 | `SKILL.md` is longer than 500 lines | **Implemented** |
| SF1004 | An unused file is present | Planned |
| SF1005 | An external URL is present | Planned |
| SF1006 | A script file exists but no permission is declared | Planned |
| SF1007 | A shell command requests broad privileges | Planned |
| SF1008 | Package dependencies are not pinned | Planned |
| SF1009 | No license is declared | **Implemented** |
| SF1010 | No agent compatibility information is declared | **Implemented** |

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
