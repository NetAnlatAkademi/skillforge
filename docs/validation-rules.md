# Validation rules

Every rule owns a stable diagnostic code. Codes are never reused or renumbered once released.

- `SF0xxx` — **Error**. The skill is not usable as written.
- `SF1xxx` — **Warning**. The skill works but quality or risk deserves attention.
- `SF2xxx` — **Info**. A neutral observation about the skill's surface.

Four further bands are reserved for the risk work planned after v0.1.0 (roadmap §30): `SF3xxx` activation and
retrieval risks, `SF4xxx` instruction injection, `SF5xxx` supply chain and provenance, `SF6xxx` version and
evolution. Nothing in them exists yet.

Through v0.1.0 the set was deliberately closed at 24 codes, which is why an unreadable `SKILL.md` widened
SF0001 rather than getting a new code. Those bands lift that constraint on purpose. The rule that does **not**
change: a published code's meaning and severity are fixed. Adding a code is cheap; redefining one breaks every
CI configuration that suppresses it.

The `Status` column tracks implementation, so the table doubles as a checklist. `Planned` means the
code is reserved but no rule exists yet.

Codes marked *(loader)* are produced while reading the skill rather than by a validation rule. The
loader reports only what prevents a skill from being modelled at all; everything else — including a
missing `name` or `description` — is a rule that runs on the loaded model.

When one mistake would trigger two codes, the more precise one wins. A duplicated frontmatter field
also makes the YAML parser fail, so SF0009 is reported and SF0003 is suppressed.

SF0001 covers both "not found" and "found but unreadable" — a locked file or a permission error reads the same
way to the person running the CLI. That widening was made when the code set was still deliberately closed; the
set is open now, but SF0001 keeps its meaning, because changing a published code is the thing that stays
forbidden.

Two codes have a defined precedence so one mistake produces one finding:

- SF0004 suppresses SF0006 and SF0007 has nothing to say about it — a skill with no name is not also
  reported as having an invalid one.
- SF0005 suppresses SF1001 and SF1002 for the same reason.
- SF0008 and SF1011 both suppress SF0007 for a given reference: a link that leaves the skill is an escape, not a
  missing file, and this rule cannot look outside the skill's own inventory anyway.
- SF0008 and SF1011 are mutually exclusive by construction — a reference either reaches a sibling or reaches
  further, never both.

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
| SF0008 | A path reaches outside the skill and its neighbours | **Implemented** (loader + rule) |
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
| SF1011 | A reference points at a sibling skill, outside this skill's own directory | **Implemented** |
| SF1012 | `skillforge.yaml` exists but could not be parsed, so its settings were ignored | **Implemented** |

## Info

| Code | Rule | Status |
|---|---|---|
| SF2001 | The skill contains a script | **Implemented** (inspect) |
| SF2002 | The skill contains an external URL | **Implemented** (inspect) |
| SF2003 | The skill contains a binary file | **Implemented** (inspect) |
| SF2004 | The skill contains an `evals` folder | **Implemented** (inspect) |

## Measured against real skills

Run over 32 skills installed on a working machine (2026-07-27), the rules behaved like this:

| Code | Skills affected |
|---|---|
| SF1010 — no compatibility declared | 32 of 32 |
| SF1009 — no license declared | 30 of 32 |
| SF1002 — description states no activation context | 3 |
| SF1003 — `SKILL.md` over 500 lines | 1 |

No errors, and nothing crashed — the loader and the error rules hold up on real input.

A second, larger run — 229 skills in one batch, including a collection where skills deliberately link to each
other — added one finding the smaller run could not show: **SF0008 fired 21 times on cross-skill references**
like `../react-testing/SKILL.md`. Those are not mistakes. A collection of skills that reference their siblings
is a real and reasonable pattern, and calling it an error fails the build over it.

The rule was still telling the truth — such a reference cannot survive being packaged on its own — but "cannot
be packaged alone" and "the author made a mistake" are different claims, and only the second deserves an error.

**This has since been fixed, and not the way it was first sketched.** The plan was to pass a collection root
into the rules so they could tell "outside the skill" from "outside the collection". That turned out to be
unnecessary: the distinction is provable from the reference text alone. One level up and back down into a named
directory *is* a sibling, by construction; two or more levels up, an absolute path, or the parent directory
itself cannot be. So no collection root, no context object threaded through every rule, and the answer is the
same whether one skill or a whole directory is being validated.

The single rule became two, keeping one code per rule: **SF1011** (warning) for a sibling reference and
**SF0008** (error) for anything reaching further. Measured again on the same 229 skills: **21 errors became 6
errors and 15 warnings**, and skills with errors went from 6 to 5. The six that remain all reach out of the
skills tree entirely — `../../ECC-Tools`, `../../rules/react/`, `../../docs/...` — which is what SF0008 is for.

The two warnings at the top are worth reading carefully. They are not finding mistakes; they are finding that
the `SKILL.md` convention in the wild does not carry `license` or `compatibility` at all. A warning that fires
on virtually every input is noise, and noise teaches people to ignore warnings. The practical consequence is
that **`--strict` fails all 32**, so it cannot be recommended as a default gate for existing skills — only for
a repository that has decided to adopt these two fields.

SF1009 and SF1010 are **not** changed in response to this measurement. Their severity is part of the published
contract, and the answer for a repository that does not want them is configuration, not a quiet downgrade.

That configuration now exists:

```bash
skillforge validate ./skills --suppress SF1009,SF1010 --strict
```

```yaml
# skillforge.yaml, per skill
validation:
  strict: false
  suppress:
    - SF1009
    - SF1010
```

Suppression is deliberately unrestricted — errors can be suppressed too, because a repository that has decided a
rule does not apply to it has a reason SkillForge cannot see. What keeps that honest is that **the count is
always reported**: `Suppressed: 2` in the console, `summary.suppressed` in JSON. A report that quietly omitted
findings would be lying about what was checked.

The difference between the two responses is worth stating: SF0008 was **wrong** about a legitimate pattern, so
it was fixed. SF1009 and SF1010 are **right** but unwanted by most existing skills, which is a configuration
problem, not a correctness one.

## What is still planned, and why it is not here yet

SF1004 to SF1008 are reserved but unimplemented, and that is a scope decision rather than an oversight.
Each needs something this release deliberately does not do:

| Code | What it would need |
|---|---|
| SF1004 (unused file) | A definition of "used" beyond a Markdown link. A script invoked by another script, or a file an agent is expected to discover by convention, would be reported as unused today — a false positive that trains people to ignore warnings. |
| SF1005 (external URL) | This is reported as an observation by `inspect` (SF2002). Promoting it to a warning means deciding that referencing a URL is a problem, which depends on policy SkillForge does not have. |
| SF1006, SF1007 (permissions, privileges) | Reading `skillforge.yaml` and cross-checking declarations against contents, plus the shell pattern matching described below. This is Milestone v0.2.0. |
| SF1008 (unpinned dependencies) | A definition of what a skill's dependencies are. Nothing in the format declares them yet. |

## Security signals

Milestone v0.2.0 detects the patterns listed in the roadmap: piped shell installers, `rm -rf`,
`Invoke-Expression`, `chmod 777`, `sudo`, privileged containers; sensitive paths such as `.env`, `.ssh`
and `/etc/`; network calls; secret-shaped identifiers.

Today `inspect` reports the neutral facts underneath those signals — that a skill ships a script, points at
a URL, or contains a binary — without interpreting them.

These checks only ever produce diagnostics. SkillForge does not classify a skill as safe or malicious
(ADR-006).
