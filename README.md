<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo.svg" alt="SkillForge" width="296" height="72">
  </picture>
</p>

# SkillForge

A local, open source CLI for the agent ecosystem's supply chain. SkillForge creates, validates, inspects and
packages `SKILL.md`-based skills — and reports where every skill, plugin and MCP server came from, how it gets its
next version, whose identity it runs with and how much it opens to an agent. Console output, JSON or SARIF, and
nothing is sent to any service.

> Status: **released as `26.249.1`** — v0.2 and v0.3 complete; v0.4's migration inventory and MCP inspection in,
> plus change control, policy-as-code and MCP allow/deny policy with `policy diff`, and now v0.5 and v0.6's
> supply-chain surface: `provenance`, `provenance diff`, `update analyze`, `identity inspect|diff` and
> `mcp surface`. Every command works end to end. CI builds and tests on Linux and Windows, and runs the CLI over
> the sample skills.

## Try it

```bash
dotnet run --project src/SkillForge.Cli -- validate ./samples/valid-skill
dotnet run --project src/SkillForge.Cli -- validate ./samples/broken-references --verbose
```

```text
SkillForge Validate

Skill: broken-references
Path:  ./samples/broken-references

x SF0007 The referenced file 'references/checklist.md' does not exist in the skill. (SKILL.md:16)
x SF0007 The referenced file 'scripts/analyze.ps1' does not exist in the skill. (SKILL.md:17)
! SF1010 No agent compatibility is declared. (SKILL.md:1)

Result: INVALID
Errors: 2  Warnings: 1  Info: 0
```

Exit codes: `0` clean · `1` validation failure, or a warning under `--strict` · `2` usage error ·
`3` unexpected failure. Options: `--strict`, `--quiet`, `--verbose`, `--no-color` (the `NO_COLOR`
environment variable works too).
> See [SKILLFORGE_ROADMAP.md](SKILLFORGE_ROADMAP.md) for scope and [TODO.md](TODO.md) for progress.

## Why

Agent skills are executable instructions that ship with real permissions. SkillForge lets a developer
notice a broken or risky skill in seconds — locally, and in CI — without sending anything to a service.

SkillForge reports concrete diagnostics and risk signals. It deliberately does **not** label a skill
"safe" or "unsafe".

## Commands

| Command | Purpose |
|---|---|
| `skillforge init <name>` | Scaffold a skill that already passes validation |
| `skillforge validate <path>` | Validate structure, frontmatter, quality rules and provider compatibility |
| `skillforge scan <path>` | The same rules, reported down to the risk signals: what it runs, reaches and asks an agent to do |
| `skillforge inspect <path>` | Summarise files, links, scripts and inferred capabilities |
| `skillforge diff <before> <after>` | Compare two versions by what they can do, not which bytes changed |
| `skillforge eval <path>` | Check a skill against the expectations declared under `evals/`, optionally by asking a model |
| `skillforge pack <path>` | Produce a deterministic `.skill.zip` with a SHA-256 hash and manifest |
| `skillforge policy check <path>` | Judge skills, and the MCP servers a configuration declares, against `.skillforge/policy.yaml` — the one command that judges rather than describes |
| `skillforge policy diff <before> <after>` | Compare two policy files and report what the later one permits that the earlier did not |
| `skillforge mcp inspect\|validate\|diff <file>` | Inspect, gate or compare an MCP configuration file |
| `skillforge mcp surface <file>` | Measure how many tools a server puts in an agent's context, and how many of them change things |
| `skillforge provenance [path]` | Report where each skill and plugin came from, and how it gets its next version |
| `skillforge provenance diff <before> <after>` | Report what drifted: publisher, marketplace, pin, fingerprint |
| `skillforge update analyze <base> <target>` | Report what an update adds, and what that means given how it arrives |
| `skillforge identity inspect\|diff <file>` | Report whose authority an agent reaches each MCP server with |
| `skillforge inventory` | Report the agent tooling installed here: skills, MCP servers and instruction files, per provider |
| `skillforge migrate inspect` | The same inventory, under the migration group |

Full options are in [docs/cli-reference.md](docs/cli-reference.md); CI usage, including SARIF upload, is in
[docs/ci.md](docs/ci.md).

### Applying an organisation's policy

Everything else describes. `policy check` judges — and only what somebody wrote down:

```yaml
# .skillforge/policy.yaml
rules:
  permissions:
    shell:
      allowed: false
    network:
      allowedDomains: ["api.github.com", "learn.microsoft.com"]
  provenance:
    requireCommitSha: true
  skills:
    requireLicense: true
```

```bash
skillforge policy check ./skills --format sarif --output artifacts/policy.sarif
```

No rule has a default that forbids anything, so an empty policy over 230 real skills produces zero findings — adopting
the command cannot start failing a build over a decision nobody made. A policy that cannot be read **fails** the run
and checks nothing, and a rule this command cannot observe says so rather than passing quietly. A suppression must
carry a reason.

An `mcp` section decides which servers may be connected to, and `--mcp <file>` applies it to a configuration:

```yaml
rules:
  mcp:
    default: deny
    allow:
      - serverUrl: "https://mcp.company.com/*"
    deny:
      - serverUrl: "http://*"
```

Deny is checked before allow, URLs are canonicalised before comparison, and a server permitted only by its display
name is reported — the name is chosen by the file under review. `default: deny` must be explicit; without it the run
fails with `SF8104` and **no** default is applied, because guessing one would bury the real problem under findings the
tool invented.

`skillforge policy diff` then answers the question a pull request asks about the policy itself: what does the later
version permit that the earlier one did not? An allow added and a deny removed are the same answer, and both are
reported as one.

### Where it came from, and what arrives next

A package hash answers "is this the file I approved". It cannot answer "will the file I approved still be the one
running tomorrow" — and with plugin marketplaces that update themselves, the answer is often no.

```bash
skillforge provenance                                     # where every asset here came from
skillforge provenance diff ./before ./after               # publisher, marketplace, pin, fingerprint
skillforge update analyze ./plugin-v1 ./plugin-v2         # what the next version would add
```

```text
Update mode: Automatic

Changes:
  + MCP server: deployment-prod
  + host: prod.company.com
  + credential source: DEPLOY_TOKEN

Risk:   HIGH
Reason: The update arrives without review (Automatic) and expands what the asset can reach.
```

The combination is what carries the meaning. A new shell script in a release somebody chose to install is ordinary;
the same script arriving overnight in a plugin that updates itself never passed a review, and a publisher change on
top of that means it did not even come from the party the organisation decided to trust.

Every value is read from a plugin manifest, a marketplace file, the git checkout or the bytes on disk, and each
asset carries the evidence it was read from. **`unknown` means nothing said it, never a guess** — an upstream is
reported only when a manifest declares one, because "this looks like that popular repository" is a guess, and a
guess in a provenance report is read as a finding. Recorded provenance is not a signature.

### Whose authority the agent is using

```bash
skillforge identity inspect ./.mcp.json
skillforge identity diff ./old/.mcp.json ./new/.mcp.json --fail-on-drift
```

```text
Identity    UserOAuth -> WorkloadIdentity
Delegated   false -> true

Widened:
  ! SF7101 'deployment-prod' is now reached with a different kind of identity: UserOAuth became WorkloadIdentity.
  ! SF7202 'deployment-prod' now acts on behalf of another party.
```

A person's OAuth session is bounded by what that person may do and disappears when they leave. A workload identity
is bounded by what the workload was granted, which is usually more, and it does not. Between two revisions of a
configuration that substitution is one line, and this is the command that reads it.

**No credential value is ever read, stored or printed.** Identity is inferred from the *names* of environment
variables and headers and from what a server's own `401` asked for — inference exactly as strong as a name, and the
output says so.

`skillforge mcp surface` answers the neighbouring question: how many tools a server puts into every conversation
before anything has decided the task needs them, and how many of those change things, govern access or reach a
secret. Deterministic, name-based, no model involved.

### Checking a skill against an agent provider

A skill is checked against the providers it declares under `compatibility`, and against nothing else — judging every
skill against every provider would report portability problems to authors who never claimed to be portable.
`--provider` asks the other question without editing the skill to find out:

```bash
skillforge validate ./skills --provider claude-code
```

`claude-code`, `codex`, `cursor` and `github-copilot` are recognised. Only `claude-code` has documented limits in
SkillForge today; the others are recognised so that declaring them is not reported as a typo, and an unread limit is
never checked rather than guessed at. The reasoning and the measurements are in
[docs/validation-rules.md](docs/validation-rules.md#provider-compatibility).

### Asking a model whether it would choose a skill

Everything above runs locally and sends nothing anywhere. One question cannot be answered that way — would an agent
actually choose this skill for this request? — so `eval` can ask a model, opt-in, per run:

```bash
skillforge eval ./my-skill --model qwen3:8b --model-endpoint http://localhost:11434/v1
```

You pick the model, local or hosted: the transport is OpenAI-compatible `/chat/completions`, which Ollama, LM Studio,
vLLM, OpenRouter and OpenAI all speak. There is no default endpoint, the API key is passed as the **name** of an
environment variable rather than a value, and the skill is judged against its siblings as distractors over several runs
so the answer is a rate rather than an anecdote. Model results carry no diagnostic code and are reported separately from
the deterministic findings. See [docs/model-runner.md](docs/model-runner.md).

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer

## Build and test

```bash
dotnet restore
dotnet build
dotnet test
```

With coverage:

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

The run settings exclude source-generated code. Without them, the matchers emitted by
`[GeneratedRegex]` dominate the numbers and hide real gaps in hand-written code.

Run the CLI from source:

```bash
dotnet run --project src/SkillForge.Cli -- --help
```

## Install as a global tool

```bash
dotnet tool install --global SkillForge.Cli
skillforge --help
```

Published on [NuGet.org](https://www.nuget.org/packages/SkillForge.Cli).

Or build and install the package locally:

```bash
dotnet pack src/SkillForge.Cli -c Release -o artifacts/local-tool

# Run this from OUTSIDE the repository — see the note below.
cd ~
dotnet tool install --global --add-source /path/to/skillforge/artifacts/local-tool SkillForge.Cli
```

The `cd` is not optional. This repository's `NuGet.config` maps every package to nuget.org, and NuGet refuses
to combine `--add-source` with source mapping — run the install from inside the repo and it fails with
`NU1110`. Running it from anywhere else, the repo's config no longer applies and the local folder is accepted.

Uninstall with `dotnet tool uninstall --global SkillForge.Cli`.

## Use it in GitHub Actions

```yaml
permissions:
  contents: read
  security-events: write # what turns findings into inline pull-request annotations

jobs:
  skills:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
      - uses: NetAnlatAkademi/skillforge@v26.249.1
        with:
          path: ./skills
          suppress: SF1009,SF1010
```

Point `path` at one skill or at a directory of them — a directory without its own `SKILL.md` is validated as a
batch, and the whole batch becomes **one** SARIF run, which is what code scanning expects. The action uploads
that SARIF itself, so findings appear as annotations on the pull request rather than only in the log.

`strict` is off by default and that is deliberate: SF1009 and SF1010 fire on almost every real skill, so a
strict run out of the box fails on skills that are fine. See [docs/validation-rules.md](docs/validation-rules.md).

Leave `version` unset and the action builds the CLI from its own checkout — slower, but it pins the CLI to the
ref you referenced the action by and works before the tool is on NuGet. Set it to a published
`SkillForge.Cli` version to install that instead.

`exit-code` is exposed as an output (0 clean, 1 findings, 2 could not run) for callers that want to decide for
themselves; the step itself still fails on anything but 0.

## Repository layout

```text
skillforge/
├── docs/         Architecture, validation rules, CLI reference
├── samples/      Example skills used by integration tests
├── src/          Domain, Application, Infrastructure, Reporting, Cli
└── tests/        One xUnit project per source project
```

Layer responsibilities and dependency rules are described in [docs/architecture.md](docs/architecture.md).

## Contributing

- Nullable reference types and warnings-as-errors are enabled; the build must stay clean.
- Every new NuGet package needs a written justification in `docs/architecture.md`.
- Validation rules live in the Application layer, never in a command class.
- Commits follow Conventional Commits, e.g. `feat(validation): validate skill frontmatter`.

## License

[MIT](LICENSE)
