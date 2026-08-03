# Running SkillForge in CI

SkillForge is built to be a build step. It exits non-zero when something is wrong, and it can produce SARIF
so findings appear as annotations on a pull request instead of being buried in a log.

## Exit codes

| Code | Meaning | What a build should do |
|---|---|---|
| 0 | No errors | Continue |
| 1 | Validation error, or a warning under `--strict` | Fail |
| 2 | The command line was wrong | Fail; fix the workflow, not the skill |
| 3 | Unexpected failure inside SkillForge | Fail and report it as a bug |

Treating 2 and 3 as separate from 1 matters: a typo in a workflow should not look like a broken skill.

## GitHub Actions

```yaml
name: Skills

on:
  pull_request:
  push:
    branches:
      - main

permissions:
  contents: read
  security-events: write   # required to upload SARIF

jobs:
  validate-skills:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "10.0.x"

      - run: dotnet tool install --global SkillForge.Cli

      - name: Validate skills
        run: skillforge validate ./skills --format sarif --output artifacts/skillforge.sarif

      - name: Upload findings
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: artifacts/skillforge.sarif
```

One command, one SARIF file, however many skills the directory holds. Point it at a directory and every skill
underneath is validated; the run fails if any single skill has errors.

Notes worth knowing before copying this:

- **`security-events: write` is required** for the upload step. Without it the workflow fails at the end
  with a permissions error and the validation result is lost.
- **`if: always()` on the upload** — findings are most useful exactly when validation failed, and a
  failed earlier step would otherwise skip the upload.
- **SARIF paths are repository-relative.** SkillForge writes the skill path relative to the working
  directory for this reason; annotations only appear when the path matches a file GitHub knows about. If
  you `cd` into a subdirectory before running it, the paths will not match.
- **`--strict` decides whether warnings block a merge.** That is a policy choice, so it is a flag rather
  than a default.

## Showing what a pull request changed about a skill

A patch shows which bytes changed. What it does not show is that a skill quietly gained a permission, a script or
a new host to talk to — which is exactly what a reviewer needs to know and the thing most likely to be waved
through.

```yaml
      - name: Check out the base for comparison
        run: git worktree add ../base origin/${{ github.base_ref }}

      - name: Diff the skill's behaviour surface
        run: |
          skillforge diff ../base/skills/my-skill ./skills/my-skill \
            --format json --output artifacts/diff.json
          skillforge diff ../base/skills/my-skill ./skills/my-skill > artifacts/diff.txt || true

      - name: Comment on the pull request
        if: github.event_name == 'pull_request'
        run: gh pr comment "${{ github.event.number }}" --body-file artifacts/diff.txt
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

`diff` exits 1 when the later version has a new **error**, so the step above uses `|| true` for the human-readable
copy and lets the JSON run decide the build. Add `--fail-on-change` if any surface change should block the merge —
that is a policy choice, which is why it is not the default.

### Annotating the change instead of commenting it

`diff --format sarif` uploads like `validate` does, so a permission the pull request adds is annotated on the file
that adds it rather than buried in a comment:

```yaml
      - name: Diff the skill's behaviour surface
        run: |
          skillforge diff ../base/skills/my-skill ./skills/my-skill             --format sarif --output artifacts/diff.sarif

      - name: Upload the diff
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: artifacts/diff.sarif
          category: skillforge-diff
```

Use a distinct `category` so the diff's results do not replace `validate`'s in code scanning.

Only the part of a diff that is a **finding** is uploaded: `SF6002` a new permission, `SF6003` a new script, `SF6004`
a new host, `SF6005` what was given up, `SF6001` growth under an unchanged version, plus any validation finding the
later revision introduced. A changed description or a new reference file stays in the console and JSON reports.

## Enforcing an organisation's policy

```yaml
      - name: Check policies
        run: |
          skillforge policy check ./skills             --policy .skillforge/policy.yaml             --format sarif --output artifacts/policy.sarif

      - name: Upload policy violations
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: artifacts/policy.sarif
          category: skillforge-policy
```

Exits 1 on a violation, on a skill that will not load, and on a policy file that cannot be read — that last one
matters in CI, because a run that checked nothing must not report success. An empty policy produces no findings, so
the step is safe to add before the rules are written. See
[cli-reference.md](cli-reference.md#skillforge-policy-check).

## Checking an MCP configuration a pull request changes

```yaml
      - name: Validate the MCP configuration
        run: skillforge mcp validate ./.mcp.json

      - name: Show what it would now connect to
        run: skillforge mcp diff ../base/.mcp.json ./.mcp.json
```

`mcp validate` gates on any finding; `mcp inspect` reports the same thing and exits 0. Neither launches a local stdio
server, and `--probe-mcp` — the only part that leaves the runner — is off unless asked for.

Taking a git range directly (`diff origin/main...HEAD`) is not implemented; the worktree above is the supported
way, and is what built-in support would do underneath.

## Consuming the JSON report

`--format json` writes the schema documented in `docs/validation-rules.md`. It is a published contract:
fields are added, never renamed or removed within a schema version.

```bash
skillforge validate ./my-skill --format json --output report.json
jq -r '.diagnostics[] | "\(.severity)\t\(.code)\t\(.message)"' report.json
```

## Packaging in CI

`pack` refuses to package a skill with errors unless `--skip-validation` is given, so a release job needs no
separate validation step. The archive is deterministic: the same contents produce the same bytes and the same
hash on any machine, which is what makes the published `.sha256` worth checking.

```bash
skillforge pack ./my-skill --output artifacts
sha256sum -c artifacts/my-skill.1.0.0.skill.zip.sha256
```
