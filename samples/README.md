# Sample skills

Fixture skills used by integration and snapshot tests, and by anyone trying the CLI for the first time.

Planned samples (added in Phase 1):

| Folder | Purpose |
|---|---|
| `valid-skill/` | Passes validation with no diagnostics |
| `invalid-frontmatter/` | Malformed YAML frontmatter — must produce SF0003, never a crash |
| `broken-references/` | References a file that does not exist — SF0007 |
| `dotnet-api-review/` | A realistic, useful skill; doubles as the documentation example |

Samples are committed fixtures. Tests must not modify them in place; copy to a temporary directory
when a test needs to mutate files.
