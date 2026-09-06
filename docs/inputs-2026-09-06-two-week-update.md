# Input — two-week ecosystem update, 2026-09-06

A record of the input that set this phase's scope, condensed from the maintainer's
`SKILLFORGE_TWO_WEEK_UPDATE_2026-09-06.md`. It is kept here for the same reason as
[inputs-2026-08-10-weekly-update.md](inputs-2026-08-10-weekly-update.md): a decision that changed the roadmap should
be traceable to whatever moved it.

**The claims below are secondhand and unverified here.** They moved the *order* of the roadmap, not the shape of any
rule. Every rule that shipped is justified by what SkillForge can observe on disk, not by this document.

## What the fortnight was said to have changed

1. **Skill provenance became a real problem.** Skills spread across GitHub by copy and by fork, so an installed
   skill's origin is frequently unknowable and its relationship to any upstream is unrecorded.
2. **Plugin marketplaces gained auto-update.** Enterprise marketplaces began supporting plugins that update
   themselves, which makes a package hash a statement about the past rather than about what will run.
3. **MCP moved towards machine identity.** Workload identity, delegated identity, token exchange and DPoP appear in
   the direction of travel, alongside progressive discovery for large tool surfaces.
4. **Tool surfaces grew.** Servers exposing well over a hundred tools are now ordinary, and every one of them sits
   in the model's context whether the task needs it or not.
5. **Evaluation and scanning got crowded.** Several new tools now score skill quality, deduplicate semantically and
   run live evals.

## What it asked for

New commands: `provenance`, `provenance diff`, `update analyze`, `identity inspect`, `identity diff`,
`mcp surface`. New domain models for `DistributionSource`, `UpdateMode`, `ProvenanceStatus` and `AgentIdentity`. New
diagnostic blocks: `SF51xx`–`SF55xx` for provenance and distribution, `SF71xx`–`SF74xx` for identity and tool
surface. A `Distribution` section in `inventory`.

Priority order it set:

```text
diff · policy · graph · provenance · update drift · MCP identity · MCP surface · external evaluator integration
```

## What it forbade

- Building a marketplace, a registry format, or a generic evaluation framework.
- Presenting a provenance guess as fact.
- Deciding auto-update behaviour from a version string alone.
- Logging identity information alongside credential content, or writing a secret into a report.
- Binding the graph model to a single vendor.

## What was implemented against it

Sprints 14–18, in `26.249.1`. See the [changelog entry](../CHANGELOG.md) for what shipped, what was decided along
the way, and what was left out on purpose.

Two of its asks were deliberately answered differently:

- **`provenance diff origin/main...HEAD`.** The document's headline invocation takes a revision range. Every
  comparison in SkillForge takes two paths, because resolving a range means materialising a tree — a worktree or a
  `git archive`, and a set of failure modes worth its own pass. [ci.md](ci.md) carries the `git worktree` recipe.
- **`update analyze --base X --target Y`.** Shipped as two positional arguments, matching `diff`, `policy diff`,
  `mcp diff` and `provenance diff`. One shape for every comparison beats one shape per document.

Also not built, and named in the document as lower priority: the `graph` node types (`Workflow`,
`ApprovalBoundary`, `Harness`, `Automation`), the agent surface score, and any external evaluator adapter.
