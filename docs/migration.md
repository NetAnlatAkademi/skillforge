# Migration inventory

`skillforge migrate inspect` reports the agent tooling installed on a machine: which providers are present, what
skills each one has, what MCP servers each one declares, and which instruction files are in play.

It **describes and does not judge** — the same stance as `inspect` (ADR-006). It exits `0` whether it finds nothing
or finds forty skills. Deciding that two instruction files contradict each other, or that one MCP server should
replace another, is reading somebody's setup for meaning, and this release does not claim to do that.

```bash
skillforge migrate inspect                      # user scope only
skillforge migrate inspect .                    # plus this project's configuration
skillforge migrate inspect . --format json      # for a script
skillforge migrate inspect --user-directory /exported/profile
```

## Environment variable values are never read

An MCP declaration is one of the likeliest places in a developer's home directory to hold an API token, and a
report that prints one has leaked it into a terminal, a CI log, or a file somebody then pastes into an issue.

So the values are **not read into the model at all**. `McpServerDeclaration` has no field for them: the readers take
the property names out of the `env` object or table and drop the values. Filtering on the way out would be one
refactor away from a leak; having nothing to filter is not. Two tests assert that a known secret value appears
nowhere in the console output or in the JSON.

For the same reason, `~/.claude/.credentials.json` and `~/.codex/auth.json` are never opened. Nothing in this
command needs a credential.

## What is read, per provider

Paths marked **verified** were checked against a working installation on 2026-07-28. Paths marked *documented* are
that provider's published convention and were not exercised, because the tool was not installed on that machine.
The failure mode of a wrong path is mild and visible: it is simply never found, so the report says the provider is
absent rather than claiming something false.

| Provider | Skills | MCP servers | Instruction files |
|---|---|---|---|
| `claude-code` | **verified** `~/.claude/skills`, *documented* `<project>/.claude/skills` | **verified** `~/.claude.json` (`mcpServers`), *documented* `<project>/.mcp.json` | **verified** `~/.claude/CLAUDE.md`, `~/.claude/AGENTS.md`; *documented* `<project>/CLAUDE.md` |
| `codex` | **verified** `~/.codex/skills` | **verified** `~/.codex/config.toml` (`[mcp_servers.*]`) | *documented* `~/.codex/AGENTS.md`, `<project>/AGENTS.md` |
| `github-copilot` | not looked for | *documented* `~/.copilot/mcp-config.json`, `<project>/.vscode/mcp.json` | *documented* `<project>/.github/copilot-instructions.md` |
| `cursor` | not looked for | *documented* `~/.cursor/mcp.json`, `<project>/.cursor/mcp.json` | *documented* `<project>/.cursorrules` |

Copilot's and Cursor's skill directories are **deliberately not guessed at**. SkillForge has read nothing about
where either would keep one, and inventing a path would produce an inventory heading that is permanently empty for
the wrong reason — indistinguishable from a provider that genuinely has no skills.

### Two facts found by looking rather than reading

- **`~/.copilot/config.json` is JSON with `//` comments.** A strict parser reports a working configuration as
  corrupt, so the JSON reader skips comments and allows trailing commas. This was found by pointing a strict parser
  at the real file and watching it fail.
- **Codex uses TOML, nobody else does.** That is why a reader declares which *format* it handles rather than which
  provider wrote the file, and why `Tomlyn` is a dependency — see `docs/architecture.md`. Tomlyn v1 parses TOML 1.1
  only; it read a real `~/.codex/config.toml` without complaint, which is the check that mattered.

## When a configuration cannot be read

**SF1015**, a warning, naming the file and the underlying reason. The rest of the inventory is still reported.

The alternative — skipping the file — would present an incomplete inventory as a complete one, and somebody
planning a migration from it would silently lose a server. Same reasoning as SF1012 for `skillforge.yaml` and
SF1014 for eval files.

## What it does not do yet

The roadmap asks for five things from this command. Three are here — skill inventory, MCP inventory and the
instruction files in play. Two are not, and each is missing for a reason rather than for lack of time:

- **Conflicting instructions.** Whether `CLAUDE.md` and `AGENTS.md` disagree is a judgement about prose. The report
  names every instruction file in play with its size and says outright that it does not judge whether they agree.
- **Missing dependencies.** Checking whether an MCP server's command exists means resolving it on `PATH`, and a
  command SkillForge fails to find is not necessarily missing — it may be installed for a shell that is not the one
  the CLI ran under. Reporting that as "missing" would be a false positive in the one place a person most needs to
  trust the output. It needs a design that can say "not found on this PATH" without implying "broken".

Provider incompatibilities are answered today by `validate --provider`, which is a sharper tool for that question
than an inventory line: see [validation-rules.md](validation-rules.md#provider-compatibility).

There is no `migrate apply`. `migrate` is a command group with one member on purpose — reading a setup and changing
one are different acts with different risks, and a future write must not be reachable by mistyping a flag on the
read.
