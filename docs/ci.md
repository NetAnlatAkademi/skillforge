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
          skillforge diff ../base/skills/my-skill ./skills/my-skill \
            --format sarif --output artifacts/diff.sarif

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
          skillforge policy check ./skills \
            --policy .skillforge/policy.yaml \
            --format sarif --output artifacts/policy.sarif

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

Add `--mcp` to judge the repository's MCP configuration against the policy's allow and deny rules in the
same step:

```yaml
      - name: Check policies
        run: |
          skillforge policy check ./skills \
            --policy .skillforge/policy.yaml \
            --mcp .mcp.json \
            --format sarif --output artifacts/policy.sarif
```

A server the policy does not permit is `SF8101` and fails the step. `default: deny` must be written down: without
it `SF8104` fails the run and no default is applied, so the report says the decision is missing rather than
inventing one finding per server.

## Reviewing a change to the policy itself

A policy decides whether other code ships, and it arrives in pull requests like anything else — where turning
`api.company.com` into `*.company.com` is a one-character diff. This reports the change of scope:

```yaml
      - name: Check out the base for comparison
        run: git worktree add ../base origin/${{ github.base_ref }}

      - name: Diff the policy
        run: |
          skillforge policy diff \
            ../base/.skillforge/policy.yaml ./.skillforge/policy.yaml \
            --format sarif --output artifacts/policy-diff.sarif

      - name: Upload what the policy now permits
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: artifacts/policy-diff.sarif
          category: skillforge-policy-diff
```

It reports **relaxations only** and exits 0 by default; add `--fail-on-weakening` to block the merge. Whether a
widened policy is acceptable is the organisation's decision, which is why making it visible and blocking it are
separate steps.

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
way, and is what built-in support would do underneath. The same applies to `provenance diff` and `update analyze`.

## Reporting what a pull request changes about where things come from

```yaml
      - name: Check out the base branch beside the workspace
        run: git worktree add ../base origin/${{ github.base_ref }}

      - name: Report distribution drift
        run: |
          skillforge provenance diff ../base ./ \
            --format sarif --output artifacts/provenance-diff.sarif

      - name: Report what an updated plugin would add
        run: skillforge update analyze ../base/plugins/deploy ./plugins/deploy

      - name: Upload the drift
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: artifacts/provenance-diff.sarif
          category: skillforge-provenance-diff
```

Both exit `0` by default and report; `--fail-on-drift` and `--fail-on-expansion` turn them into gates. Whether a
changed publisher blocks a merge is the organisation's decision, which is why making it visible and blocking it are
separate steps.

`update analyze` is the one to gate first if only one is gated: a publisher change under an auto-updating plugin is
`CRITICAL`, and it is the case where the review that would have caught it is precisely the review that does not
happen.

## Putting the wiring diagram in the pull request

`graph --format mermaid` renders in a GitHub comment without a toolchain, so a reviewer looking at a change to
`.mcp.json` can see what it rewired.

```yaml
      - name: Draw the wiring
        run: |
          {
            echo '## What this repository is wired to'
            echo
            echo '```mermaid'
            skillforge graph . --format mermaid
            echo '```'
          } > graph.md

      - name: Comment
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: fs.readFileSync('graph.md', 'utf8'),
            });
```

`graph` exits `0` and makes no network request, so it is safe in a job with no egress. It fails only on an MCP
configuration it could not parse (`SF1015`) — a node missing from a diagram because a file would not read is worse
than a stated gap.

Two runs over unchanged input produce byte-identical output, which means the diagram can be committed and diffed:

```bash
skillforge graph . --format mermaid --output docs/wiring.mmd
git diff --exit-code docs/wiring.mmd   # non-zero when the wiring changed
```

## Verifying a registry's claims against the servers behind them

```yaml
      - name: Verify discovered MCP servers
        run: |
          skillforge discovery verify "" \
            --registry https://registry.internal.example/v0/servers \
            --kind mcp-registry \
            --probe \
            --format sarif \
            --output artifacts/discovery-drift.sarif

      - name: Upload
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: artifacts/discovery-drift.sarif
          category: skillforge-discovery
```

Three things worth deciding deliberately before adding this step:

- **It makes network requests, and only the ones you asked for.** One request to the registry, plus one probe per
  discovered remote HTTP MCP server. There is no default registry, so a job that does not name one makes no request
  at all. A local stdio server is never launched.
- **`--probe` is what turns a metadata read into a fleet scan.** Without it the step reads the registry and reports
  every resource as not probed. On a registry with hundreds of servers, narrow the query or set `--limit` before
  adding `--probe`.
- **`--fail-on-drift` is a separate decision.** The step above reports and exits `0`. Whether a tool the registry
  never listed blocks a merge is the organisation's call, and `policy check --mcp` is where that call belongs — the
  drift finding describes the difference and does not decide whether it is allowed.

Only the findings go into the SARIF. The resource listing is a search result, and uploading it as static-analysis
results would put an annotation on the pull request for every server the registry happens to hold. Use
`--format json` when the listing itself is what a later step needs.

The SARIF carries `SF8201` (a runtime tool the registry never declared) as a warning, and `SF8202`–`SF8204` as
notes. `SF1016` means the registry could not be read at all, which fails the run whatever `--fail-on-drift` says:
"no results" and "no answer" are different facts.

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
