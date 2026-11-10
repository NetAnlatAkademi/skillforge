# SkillForge — Master Coding Agent Prompt
## Consolidated development plan after v0.8
### Updated: 2026-11-07

**Repository:** `NetAnlatAkademi/skillforge`  
**Observed main branch:** `main`  
**Latest visible commit:** `deb49a50` — 2026-09-07  
**Current README release:** `26.250.1`  
**Current implemented milestone:** `v0.8`

---

# 0. Your Role

You are the coding agent responsible for continuing SkillForge.

Do not treat this file as a brainstorming document.

Treat it as an implementation brief.

Before making any change:

```bash
git status
git log -10 --oneline
```

Then read:

```text
README.md
TODO.md
CHANGELOG.md
SKILLFORGE_ROADMAP.md
docs/architecture.md
docs/validation-rules.md
docs/discovery.md
docs/graph.md
docs/ci.md
```

Also inspect the current implementation before creating a new abstraction.

If something requested here has already been implemented since this document was generated:

> Do not implement it again. Verify it, document that it is already satisfied, and continue with the next missing item.

---

# 1. Product Thesis

SkillForge is NOT:

```text
registry
marketplace
installer
package manager
sandbox
generic vulnerability scanner
generic AI security scanner
LLM gateway
agent runtime
```

SkillForge IS:

> A vendor-neutral security and governance layer for Agent Skills, Agent Plugins, MCP, agent instructions, workflows and their supply chain.

The core value proposition is:

```text
What was declared?
Where did it come from?
What actually resolved?
What content actually arrived?
What does runtime actually expose?
Which policy actually wins?
What does runtime actually permit?
What changed from the approved baseline?
```

The product should optimize for:

```text
provenance
drift
effective policy
runtime verification
composition graph
supply-chain evidence
```

Do not respond to competitors by adding hundreds of generic lint rules.

---

# 2. Current Repository Baseline — Already Implemented

The current repository already contains:

```text
skillforge init
skillforge validate
skillforge scan
skillforge inspect
skillforge diff
skillforge eval
skillforge pack

skillforge policy check
skillforge policy diff

skillforge mcp inspect
skillforge mcp validate
skillforge mcp diff
skillforge mcp surface

skillforge provenance
skillforge provenance diff
skillforge update analyze

skillforge identity inspect
skillforge identity diff

skillforge graph

skillforge discover
skillforge discovery verify

skillforge inventory
skillforge migrate inspect
```

Existing architecture also already includes:

```text
MCP 2025-11-25 adapter
MCP 2026-07-28 adapter
full tools/list pagination
bounded MCP probing
MCP Registry adapter
ARD adapter
declared-vs-runtime MCP tool verification
asset graph
ApprovalBoundary nodes
provenance
update drift
identity drift
MCP surface analysis
policy-as-code
MCP allow/deny
policy diff
SARIF
GitHub Action
OpenAI-compatible optional eval model runner
```

---

# 3. Do NOT Reimplement

Do not rebuild:

```text
graph foundation
ARD discovery
MCP Registry discovery
tools/list pagination
discovery verify for MCP tools
ApprovalBoundary base graph support
provenance foundation
provenance diff
update analyze
identity inspect/diff
mcp surface
MCP protocol adapters
policy check
policy diff
generic SARIF support
GitHub Action
generic skill scanner
generic eval framework
```

Always extend the existing implementation.

Avoid parallel engines.

---

# 4. New Development Priorities

Implement in this order:

```text
P0 — Skills-over-MCP support + integrity verification
P0 — Git pin-resolution / resolved-content verification

P1 — APM manifest + lockfile supply-chain adapter
P1 — Security Advisory Intelligence
P1 — Effective Policy Resolution

P2 — Managed Connector Inventory
P2 — Runtime Policy Evidence
P2 — Usage Evidence + Evidence Quality
P2 — Agent Skills specification/version seam

P3 — Workflow/Harness/Automation graph detectors
P3 — direct git revision-range support
```

Do not start lower-priority items before the P0 items are stable.

---

# 5. P0 — Skills-over-MCP

## Background

The official MCP Skills extension is now stable.

Extension:

```text
io.modelcontextprotocol/skills
```

Base MCP revision:

```text
2026-07-28
```

Important methods:

```text
skills/list
skills/get
resources/read
resources/directory/read
```

Current SkillForge supports MCP tool discovery but does not currently implement the Skills extension.

Repository search should confirm the following are still absent before coding:

```text
skills/list
skills/get
io.modelcontextprotocol/skills
resources/directory/read
```

---

# 6. Design Principle

Do not create another SKILL.md parser.

MCP Skills is a TRANSPORT.

Agent Skills is the CONTENT FORMAT.

Architecture:

```text
MCP transport
    ↓
skills/list
skills/get
resources/read
    ↓
materialized skill
    ↓
existing SkillLoader
existing validation
existing inspect
existing provenance
```

Reuse the current SkillForge skill model wherever possible.

---

# 7. MCP Skills Client Abstraction

Suggested seam:

```csharp
public interface IMcpSkillsClient
{
    ValueTask<McpSkillListResult> ListAsync(
        McpServerDeclaration server,
        CancellationToken cancellationToken = default);

    ValueTask<McpSkillEntry> GetAsync(
        McpServerDeclaration server,
        Uri skillUri,
        CancellationToken cancellationToken = default);

    ValueTask<McpResourceContent> ReadAsync(
        McpServerDeclaration server,
        Uri resourceUri,
        CancellationToken cancellationToken = default);
}
```

Optional:

```csharp
ValueTask<McpDirectoryResult?> ReadDirectoryAsync(...)
```

only when declared by the server.

Do not make the existing protocol adapters giant classes.

Use the existing negotiation/transport infrastructure.

---

# 8. `skills/list`

Requirements:

```text
full pagination
bounded page count
cursor-loop detection
CancellationToken between requests
deterministic ordering
duplicate skill URI handling
malformed entry isolation
no implicit network
```

Use the same bounded-walk philosophy as `tools/list`.

Do not invent different safety behavior.

---

# 9. `skills/get`

Capture:

```text
skill URI
frontmatter
resource manifest
ttlMs
cacheScope
extension metadata
```

A skill does NOT have to appear in `skills/list` before `skills/get`.

Do not flag that alone as malicious or invalid.

---

# 10. Static Skill Integrity

For a static manifest:

```text
skills/get
   ↓
manifest
   ↓
resources/read
   ↓
actual bytes
   ↓
SHA-256 + size
   ↓
compare
```

Verify:

```text
digest algorithm
digest syntax
content digest
declared size
actual size
```

Successful verification means:

```text
INTEGRITY MATCH
```

It does NOT mean:

```text
safe
trusted
verified publisher
secure
```

Keep these concepts separate:

```text
Integrity
Provenance
Trust
Policy
Behavior
```

---

# 11. SKILL.md Frontmatter Drift

Compare:

```text
skills/get frontmatter
```

against:

```text
SKILL.md frontmatter read from resources/read
```

Reuse the existing frontmatter parser.

Report comparable field drift.

Do not create a second YAML parser pipeline.

---

# 12. Manifest Boundary

For static skills:

```text
manifest == approved resource set
```

If directory enumeration shows a file absent from the manifest:

```text
UNLISTED RESOURCE
```

If a manifest file cannot be read:

```text
MISSING RESOURCE
```

Do not silently add unlisted resources to the approved set.

---

# 13. Dynamic Skills

When:

```text
resources = dynamic
```

report:

```text
Static content integrity cannot be established.
```

Dynamic does not mean invalid.

Do not fabricate a hash baseline.

---

# 14. MCP-Served Skill Provenance

Extend existing provenance so the source can be:

```text
LocalFilesystem
GitRepository
Plugin
Marketplace
McpServer
Unknown
```

For MCP-served skills record, when evidence exists:

```text
server endpoint
server self-reported version
MCP protocol version
skill URI
manifest digest data
observed timestamp
```

Do not claim server identity is cryptographically verified unless it actually is.

---

# 15. CLI Surface

Prefer:

```bash
skillforge mcp skills inspect <mcp-config> --probe
skillforge mcp skills verify <mcp-config> --probe
```

If the existing command tree makes this awkward, choose the cleanest non-conflicting equivalent.

Do not create five new root commands.

System.CommandLine command-name collision behavior must have integration tests.

---

# 16. MCP Skills Diagnostics

Choose actual diagnostic numbers only after inspecting the current published ranges.

Suggested semantic findings:

```text
MCP_SKILL_DIGEST_MISMATCH
MCP_SKILL_SIZE_MISMATCH
MCP_SKILL_FRONTMATTER_DRIFT
MCP_SKILL_UNLISTED_RESOURCE
MCP_SKILL_MISSING_RESOURCE
MCP_SKILL_INVALID_MANIFEST
MCP_SKILL_DYNAMIC_INTEGRITY_LIMIT
MCP_SKILLS_PAGING_INCOMPLETE
```

Never renumber an existing published code.

---

# 17. Required MCP Skills Tests

```text
skills-list-single-page
skills-list-multiple-pages
skills-list-empty
skills-list-cursor-loop
skills-list-page-limit
skills-get-direct
skills-get-not-listed
static-valid
static-digest-mismatch
static-size-mismatch
frontmatter-drift
unlisted-file
missing-file
dynamic-skill
directory-read-supported
directory-read-not-supported
invalid-digest
oversized-resource
timeout
cancellation
```

---

# 18. P0 — Git Pin Resolution / Plugin Supply Chain

## Background

A major supply-chain lesson is:

```text
Declared SHA
!=
Resolved content
```

A plugin can claim to be pinned while the actual resolved tree/content differs from what a reviewer believes was approved.

Current provenance already records repository/commit/fingerprint, but it does not explicitly model:

```text
DeclaredRef
DeclaredCommit
ResolvedRef
ResolvedCommit
TreeDigest
```

Add that distinction.

---

# 19. Git Resolution Evidence

Suggested model:

```csharp
public sealed record GitResolutionEvidence(
    string? DeclaredRef,
    string? DeclaredCommit,
    string? ResolvedRef,
    string? ResolvedCommit,
    string? TreeDigest,
    string? ContentDigest,
    string? RepositoryUrl);
```

Rules:

```text
unknown stays unknown
no declared ref => do not invent one
no network unless explicitly requested
```

---

# 20. Questions SkillForge Must Answer

```text
What ref did the manifest request?
What commit did that ref resolve to?
What tree/content was actually installed?
Did the repository origin change?
Did the declared pin stay constant while content changed?
Did an automatic update expand capabilities?
```

Integrate with:

```text
provenance
provenance diff
update analyze
graph
```

Do not create a second supply-chain engine.

---

# 21. New Provenance Risk Cases

Detect evidence-backed cases:

```text
DeclaredCommit != ResolvedCommit
DeclaredRef resolves differently
Declared SHA unchanged but tree/content changed
Origin repository changed
Pinned → floating
Tag → branch
Publisher changed
Marketplace changed
Automatic update + capability expansion
Automatic update + publisher drift
```

Do not label a repository malicious merely because of a mismatch.

---

# 22. Remote Resolution

Possible UX:

```bash
skillforge provenance verify ./plugin --resolve
```

or:

```bash
skillforge provenance ./plugin --resolve
```

Pick the smallest API.

Rules:

```text
explicit opt-in
bounded timeout
bounded Git process
sanitized URLs
no credential values
no credential helper output
```

Reuse the existing URL credential sanitization.

---

# 23. P1 — Agent Package Manager (APM) Integration

## Why this is now important

Microsoft APM has become a serious packaging/distribution layer for agent configuration.

It uses:

```text
apm.yml
apm.lock.yaml
```

and can manage:

```text
skills
prompts
instructions
plugins
MCP servers
transitive dependencies
multiple agent targets
```

GitHub Dependabot now supports APM version updates.

Therefore APM is now a real supply-chain input for SkillForge.

SkillForge must NOT become a package manager.

SkillForge should ANALYZE APM.

---

# 24. Do Not Compete With APM

Do NOT implement:

```text
apm install
package resolution
target deployment
agent configuration compilation
marketplace installation
transitive dependency installation
```

APM already solves these.

SkillForge should answer:

```text
What does this APM dependency graph introduce?
What changed in the lockfile?
What new MCP server appeared?
What new skill/script/hook arrived?
What package/publisher/ref/hash changed?
What does a Dependabot APM update actually expand?
```

---

# 25. APM Adapter

Add readers:

```text
ApmManifestReader
ApmLockfileReader
```

Suggested abstraction:

```csharp
public interface IAgentPackageManifestReader
{
    bool CanRead(string path);

    ValueTask<AgentPackageManifest> ReadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
```

Do not hard-code APM concepts throughout Domain.

Normalize them.

---

# 26. Normalized Package Dependency Model

Suggested:

```csharp
public sealed record AgentPackageDependency(
    string Id,
    AgentPackageDependencyType Type,
    string? Source,
    string? DeclaredVersion,
    string? ResolvedVersion,
    string? DeclaredRef,
    string? ResolvedCommit,
    string? ContentHash,
    IReadOnlyList<string> Targets);
```

Types:

```text
Skill
Plugin
Instructions
Prompt
McpServer
Package
Unknown
```

---

# 27. APM Lockfile Analysis

For:

```text
apm.lock.yaml
```

extract:

```text
resolved package version
git commit/ref
content hash
transitive dependencies
MCP dependencies
target mappings
```

Then feed into:

```text
provenance
update analyze
graph
```

Do not build an APM-only reporting silo.

---

# 28. Dependabot + SkillForge Workflow

GitHub can now open Dependabot PRs for APM dependencies.

This creates an excellent SkillForge CI use case:

```text
Dependabot updates apm.yml / apm.lock.yaml
        ↓
SkillForge analyzes base vs PR
        ↓
new skills
new MCP servers
new hosts
new scripts
publisher changes
hash/ref changes
capability expansion
        ↓
SARIF / PR finding
```

Design the implementation so this works naturally.

Preferred outcome:

```bash
skillforge update analyze ./base ./target
```

understands APM without needing:

```bash
skillforge apm-special-diff
```

unless there is a compelling architecture reason.

---

# 29. APM Policy / Trust Input

APM has install-time policy concepts and transitive MCP controls.

SkillForge should be able to READ relevant exported/local policy evidence, but should not reproduce APM enforcement.

Graph relationships may include:

```text
Package
  Contains → Skill
  DependsOn → Package
  Declares → MCP Server
  DistributedBy → Marketplace
```

This is a strong extension of the current graph.

---

# 30. APM Tests

Required fixtures:

```text
apm-simple-skill
apm-multiple-targets
apm-plugin
apm-transitive-package
apm-transitive-mcp
apm-lockfile
apm-lockfile-hash-change
apm-lockfile-ref-change
apm-dependabot-update
apm-package-adds-mcp
apm-package-removes-mcp
apm-package-pinned-to-floating
```

---

# 31. P1 — Security Advisory Intelligence

## Goal

Correlate known advisories with the actual SkillForge inventory.

Do NOT implement exploit testing.

A security advisory can depend on:

```text
package
version
transport
authentication
platform
feature configuration
```

A version-only matcher is insufficient.

---

# 32. Advisory Domain Model

Suggested:

```csharp
public sealed record SecurityAdvisory(
    string AdvisoryId,
    string? Cve,
    string Product,
    VersionRange? AffectedVersions,
    string? FixedVersion,
    IReadOnlyList<AdvisoryCondition> Conditions,
    Uri Source,
    AdvisorySeverity Severity,
    DateTimeOffset PublishedAt);
```

CVE is optional.

Important incidents may exist before CVE assignment.

---

# 33. Advisory Conditions

Support conditions such as:

```text
TransportIs
AuthenticationIs
PlatformIs
FeatureEnabled
ConfigurationPresent
ConfigurationMissing
VersionInRange
```

Result:

```text
Affected
NotAffected
PotentiallyAffected
Unknown
```

Unknown must remain Unknown.

---

# 34. Initial Advisory Fixture

Include the AWS API MCP Server advisory:

```text
Product:
awslabs.aws-api-mcp-server

Affected:
>= 1.3.0
< 1.6.0

Relevant:
streamable-http
OAuth

Fixed:
1.6.0

Advisory:
GHSA-4prq-p2x5-w9fh
CVE-2026-115413
```

If the relevant audience/client-ID configuration cannot be observed:

```text
PotentiallyAffected
```

not:

```text
Affected
```

---

# 35. Advisory Sources

First version:

```text
local/bundled advisory catalog
```

Future explicit adapters:

```text
GitHub Security Advisories
OSV
vendor advisories
```

No hosted SkillForge vulnerability backend yet.

No implicit external requests.

---

# 36. Suggested Command

Prefer:

```bash
skillforge advisory check [path]
```

This leaves room for:

```text
MCP advisories
plugin advisories
skill distribution advisories
agent runtime advisories
```

---

# 37. P1 — Effective Policy Resolution

## Problem

Agent policy is now layered:

```text
Provider Global
Enterprise
Organization
Repository
Workspace
User
Runtime
```

The configuration visible in a repository may not be the policy actually enforced.

SkillForge should calculate:

```text
Effective Policy
```

without becoming a policy enforcement engine.

---

# 38. Policy Layer Model

Suggested:

```csharp
public enum PolicyLayerType
{
    ProviderGlobalDefault,
    Enterprise,
    Organization,
    Repository,
    Workspace,
    User,
    Runtime,
    Unknown
}
```

```csharp
public sealed record PolicyLayer(
    PolicyLayerType Type,
    string Source,
    int Precedence,
    IReadOnlyDictionary<string, PolicyValue> Values);
```

---

# 39. Effective Policy Evidence

Every effective value should answer:

```text
What is effective?
Which layer supplied it?
Which lower-precedence values were overridden?
Was the setting inherited?
Was policy freshness confirmed?
```

Model:

```text
EffectiveValue
WinningLayer
OverriddenValues
SourceEvidence
Freshness
```

---

# 40. CLI

Suggested:

```bash
skillforge policy effective
```

First version reads explicit local/exported files.

Do not call GitHub or another provider's cloud API automatically.

Example:

```bash
skillforge policy effective \
  --enterprise enterprise.json \
  --repository repository.json \
  --user user.json
```

---

# 41. Effective Policy Diff

After effective resolution is stable, extend semantic policy diff.

High-value finding:

```text
Effective permission widened even though repository policy did not change.
```

Do not build a second diff engine.

Reuse `PolicyDiffer` patterns.

---

# 42. Policy Freshness

Enterprise providers increasingly support fail-closed policy refresh.

Model:

```text
PolicySource
LastConfirmedAt
FreshnessStatus
FailClosed
MinimumClientVersion
```

Do not implement remote refresh yet.

Only carry evidence when supplied.

---

# 43. P2 — Managed Connector Inventory

## Problem

An MCP server may now be connected through a provider-managed connector and not exist in a local config file.

Therefore:

```text
No local config
```

does NOT imply:

```text
No MCP connection
```

---

# 44. Connector Source Model

Add:

```csharp
public enum ConnectorSource
{
    LocalConfiguration,
    RepositoryConfiguration,
    EnterpriseManaged,
    ProviderManaged,
    RuntimeDiscovered,
    Unknown
}
```

Only report provider-managed connectors when real evidence exists.

Never guess.

---

# 45. Managed Connector Adapter

Future seam:

```csharp
public interface IManagedConnectorInventoryAdapter
{
    string Provider { get; }

    ValueTask<IReadOnlyList<ManagedConnector>> ReadAsync(
        CancellationToken cancellationToken = default);
}
```

Do not implement scraping.

Implement a provider adapter only when a stable supported API/export exists.

---

# 46. Inventory Semantics

Use:

```text
Not observed
```

instead of:

```text
None
```

when an inventory source could not be queried.

This distinction is important.

---

# 47. P2 — Runtime Policy Evidence

## Goal

Analyze runtime enforcement evidence without becoming a runtime.

Possible sources include:

```text
OpenShell
provider sandbox exports
enterprise sandbox policy
container policy
microVM policy
```

Core must remain vendor-neutral.

---

# 48. Runtime Evidence Model

Use the existing `ExecutionEnvironment` graph type.

Suggested:

```csharp
public enum ExecutionEnvironmentType
{
    Unknown,
    Local,
    Container,
    MicroVm,
    RemoteSandbox
}
```

```csharp
public sealed record RuntimePolicyEvidence(
    ExecutionEnvironmentType Environment,
    string? Provider,
    FileSystemPolicyEvidence? FileSystem,
    NetworkPolicyEvidence? Network,
    CredentialPolicyEvidence? Credentials,
    ApprovalPolicyEvidence? Approval,
    string Source);
```

---

# 49. Effective Capability

Future calculation:

```text
Declared Capability
        ∩
Effective Policy
        ∩
Runtime Enforcement
        =
Effective Capability
```

Only compute a layer when evidence exists.

Missing runtime evidence means:

```text
Unknown
```

not:

```text
Allowed
```

or:

```text
Denied
```

---

# 50. P2 — Usage Evidence

GitHub now exposes named usage for:

```text
skills
custom agents
MCP servers
plugins
slash commands
```

It also exposes BYOK/custom-model usage and telemetry/version gaps.

This creates an optional enterprise evidence layer.

Do not confuse:

```text
no telemetry
```

with:

```text
zero usage
```

---

# 51. Usage Evidence Model

Suggested:

```csharp
public sealed record UsageEvidence(
    string AssetId,
    long? UsageCount,
    DateOnly? From,
    DateOnly? To,
    string Source,
    EvidenceCompleteness Completeness);
```

```csharp
public enum EvidenceCompleteness
{
    Complete,
    Partial,
    MissingTelemetry,
    UnsupportedClientVersion,
    Unknown
}
```

This is the important part.

A risk ranking built on incomplete telemetry must say so.

---

# 52. Usage Context in Graph

Future graph context:

```text
Capability
├── Risk
├── Provenance
├── EffectivePolicy
└── UsageEvidence
```

Example:

```text
prod-admin-mcp
Risk: HIGH
Usage: 6,421
Evidence: Complete

legacy-root-mcp
Risk: HIGH
Usage: 0
Evidence: MissingTelemetry
```

The second must NOT be interpreted as unused.

---

# 53. BYOK / Custom Model Evidence

BYOK is not a core SkillForge feature.

But model/provider choice can be relevant runtime evidence.

If modeled later:

```text
ModelSource
├── ProviderHosted
├── EnterpriseCustom
├── LocalBYOK
├── RemoteBYOK
└── Unknown
```

Keep it P3 unless it becomes necessary for policy analysis.

Do not build spend analytics.

---

# 54. P2 — Agent Skills Specification Governance

Agent Skills is now under vendor-neutral AAIF/Linux Foundation governance.

Expect:

```text
versioned releases
formal change process
security guidance
```

Do not invent versions before they are published.

Prepare the architecture.

---

# 55. Specification Profile

Only introduce when needed:

```csharp
public sealed record SkillSpecificationProfile(
    string Identifier,
    string? Version,
    Uri? SpecificationUri);
```

Today:

```text
unknown/unversioned
```

is valid if no version is declared.

---

# 56. Rule Authority Metadata

Validation rules should eventually identify their authority:

```text
SkillForgeOpinion
AgentSkillsSpecification
McpSpecification
ProviderDocumentation
OrganizationPolicy
```

Example:

```text
SFxxxx
Source: Agent Skills specification
Reference: <spec section>
```

This becomes increasingly valuable once versions diverge.

Do not change the meaning of existing diagnostic codes.

---

# 57. P3 — Workflow / Harness / Automation Graph Detectors

The domain already has placeholders for:

```text
Workflow
Harness
Automation
ExecutionEnvironment
```

Only add detectors from structured evidence.

Do NOT infer these from prose.

Possible future sources:

```text
GitHub Agentic Workflow definitions
provider automation manifests
runtime export files
agent harness configuration
```

Lower priority than supply-chain work.

---

# 58. Existing Deferred Work

Do not lose existing deliberate debt:

```text
skillforge diff origin/main...HEAD
```

still uses two filesystem paths.

Also still deliberately deferred:

```text
policy enforcement for mcp.allowedProtocolVersions
policy enforcement for mcp.denyDeprecatedCapabilities
pack provenance signing
ProductionImpactingPath + NoExplicitApprovalBoundary rule
endpoint drift after redirects
```

Do not automatically mix these into the new work.

Solve them only when new architecture provides a clean evidence path.

---

# 59. What NOT to Build

Keep these out:

```text
SkillForge marketplace
SkillForge registry
package installer
APM replacement
sandbox runtime
agent runtime
MCP execution host
hosted vulnerability scanner backend
generic LLM security scanner
generic skill quality score
vector search
semantic marketplace search
auto installation
automatic stdio execution
implicit internet access
provider scraping
```

---

# 60. Competitive Positioning

The ecosystem now has:

```text
Microsoft APM
Dependabot APM updates
MCP Inspector
GitHub AI security scanning
JFrog AI security
NVIDIA OpenShell
provider-managed MCP connectors
enterprise plugin policy
```

This means these are becoming commodity:

```text
installation
marketplace
generic scanning
basic lint
sandbox enforcement
```

SkillForge should focus on the cross-cutting layer:

```text
Provenance
Resolved Content
Update Drift
Runtime Drift
Effective Policy
Composition Graph
Advisory Correlation
Evidence Quality
```

---

# 61. Target Architecture

The long-term analysis pipeline should become:

```text
Declared Asset
      ↓
Package / Manifest
      ↓
Dependency Graph
      ↓
Distribution / Provenance
      ↓
Resolved Ref / Commit
      ↓
Actual Content
      ↓
Runtime Capability
      ↓
Effective Policy
      ↓
Runtime Enforcement
      ↓
Security Advisory Context
      ↓
Usage Evidence
      ↓
Effective Risk / Drift Evidence
```

Each stage must preserve source evidence.

No evidence → no claim.

---

# 62. Implementation Sequence

Execute in this exact order unless repository state proves an item already exists:

```text
1. MCP Skills extension transport
2. MCP Skills integrity verification
3. MCP-served skill provenance

4. Git declared-ref vs resolved-content verification
5. Integrate resolution evidence with provenance diff/update analyze

6. APM manifest reader
7. APM lockfile reader
8. APM dependencies in graph/provenance/update analyze
9. Dependabot APM PR analysis scenario

10. Security Advisory domain + local catalog
11. Advisory matching against inventory/config

12. Effective Policy model
13. Effective Policy resolver
14. Effective Policy semantic diff

15. Managed Connector source model
16. Managed Connector adapter seam

17. Runtime Policy Evidence model
18. Effective Capability composition seam

19. UsageEvidence + EvidenceCompleteness

20. Agent Skills specification/version seam
21. Rule authority/source metadata

22. Structured Workflow/Harness/Automation detectors
```

---

# 63. Coding Standards

Preserve existing SkillForge principles:

```text
modular monolith
local-first
no database
no hosted backend
no implicit network
no secrets in output
CancellationToken on async APIs
TimeProvider for time
deterministic output
bounded remote reads
one type per file
evidence-backed diagnostics
```

Do not add infrastructure simply because it might be useful later.

---

# 64. Security Rules

Every external input is untrusted:

```text
registry JSON
MCP responses
SKILL.md
APM manifests
lockfiles
plugin manifests
Git metadata
advisory documents
runtime policy exports
usage exports
```

Guard:

```text
maximum response/body size
maximum page count
maximum JSON depth
timeouts
redirect limits
path traversal
symlink escape
credential leakage
cursor loops
dependency cycles
graph cycles
```

---

# 65. Secret Handling

Never output:

```text
API keys
OAuth tokens
Authorization headers
environment variable values
Git credentials
URL user-info
credential helper output
private token payloads
```

Names are acceptable only where current architecture already permits them:

```text
DEPLOY_TOKEN
GITHUB_TOKEN
AWS_PROFILE
```

Values are never acceptable.

---

# 66. Diagnostic Policy

Before publishing a new diagnostic:

```text
1. define one precise meaning
2. build fixtures
3. measure prevalence
4. select severity
5. document evidence
6. document false-positive boundary
```

Do not publish a warning that fires on nearly every real repository.

Do not reuse an existing code for a new meaning.

Published meanings are immutable.

---

# 67. Reporting Policy

Use:

```text
Console
JSON
SARIF
```

appropriately.

Rule:

```text
Description/inventory → Console + JSON
Actionable finding → SARIF allowed
Graph → Console + JSON + Mermaid
```

Do not put every informational observation into SARIF.

---

# 68. Exit Behavior

Preserve the existing philosophy:

```text
inspect/inventory/graph describe
policy judges
verify reports evidence/drift
```

Do not make descriptive commands fail because something looks risky unless the command explicitly supports a fail flag.

---

# 69. Required Validation Before Finishing Each Milestone

Run:

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

Also ensure CI remains green on:

```text
Linux
Windows
```

For CLI changes, run end-to-end command tests.

For JSON changes, maintain backward-compatible additive shapes where practical.

---

# 70. Documentation Required

For every milestone update, review:

```text
README.md
TODO.md
CHANGELOG.md
SKILLFORGE_ROADMAP.md
docs/architecture.md
docs/cli-reference.md
docs/validation-rules.md
docs/ci.md
```

Add dedicated docs when justified:

```text
docs/mcp-skills.md
docs/apm.md
docs/advisories.md
docs/effective-policy.md
docs/runtime-evidence.md
```

Do not create empty docs.

---

# 71. First Coding Task

Start with this task only.

```text
Inspect the current repository first.

Confirm that MCP Skills support is still absent.

Implement official Skills-over-MCP support using
io.modelcontextprotocol/skills.

Do not reimplement MCP transport or SKILL.md parsing.

Deliver:
- skills/list with bounded pagination
- skills/get
- resources/read
- resources/directory/read when declared
- static resource manifest verification
- SHA-256 + size validation
- SKILL.md frontmatter consistency validation
- dynamic skill integrity limitation reporting
- MCP-served skill provenance
- console + JSON
- SARIF only for actual integrity findings
- full fixtures/tests
- updated documentation

Never launch stdio servers automatically.
Never call network without explicit probe/input.
Never call integrity 'trust' or 'safety'.

After implementation:
dotnet format --verify-no-changes
dotnet build
dotnet test

Then report:
- changed files
- new domain types
- new diagnostics
- new commands/options
- tests added
- any spec ambiguity encountered
- any follow-up work deliberately deferred
```

---

# 72. After Task 1

Do not immediately invent more work.

Re-read:

```text
TODO.md
CHANGELOG.md
SKILLFORGE_ROADMAP.md
```

Then continue with:

```text
Git pin-resolution
```

followed by:

```text
APM adapter
```

in that order.

---

# 73. New Developments Added Since the Previous Consolidated Prompt

The following items are specifically new since the previous consolidated update:

## 73.1 Agent Package Manager became a first-class supply-chain input

Microsoft APM now provides:

```text
apm.yml
apm.lock.yaml
transitive agent dependencies
skills
plugins
prompts
instructions
MCP dependencies
multi-agent target deployment
policy
content hashes
```

This creates a new analysis target for SkillForge.

## 73.2 Dependabot now updates APM

GitHub Dependabot can now:

```text
update APM git dependencies
open PRs
regenerate apm.lock.yaml
```

This makes SkillForge's existing PR/SARIF model especially valuable for APM update reviews.

## 73.3 Usage telemetry now has evidence-quality signals

GitHub metrics now expose:

```text
client telemetry present/missing
supported/unsupported client versions
named skill/MCP/plugin usage
BYOK/custom model usage
```

Therefore usage evidence must carry completeness metadata.

Missing telemetry must never be treated as zero use.

## 73.4 No new MCP protocol adapter is currently required

The stable Skills extension still targets:

```text
MCP 2026-07-28
```

No new base protocol revision currently requires replacing the existing adapter strategy.

## 73.5 MCP Registry does not require a new adapter rewrite

No new breaking Registry API revision was identified that invalidates the current SkillForge adapter.

Do not rewrite it without evidence.

---

# 74. Primary References

Use these as source material when implementing.

```text
MCP Skills:
https://github.com/modelcontextprotocol/ext-skills
https://github.com/modelcontextprotocol/ext-skills/blob/main/specification/stable/skills.mdx
https://github.com/modelcontextprotocol/ext-skills/blob/main/docs/threat-model.md

Agent Package Manager:
https://github.com/microsoft/apm
https://microsoft.github.io/apm/

Dependabot APM:
https://github.blog/changelog/2026-11-03-dependabot-version-updates-support-agent-package-manager/

Agent Skills / AAIF:
https://www.linuxfoundation.org/press/agentic-ai-foundation-announces-four-new-projects-agent-skills-open-responses-openenv-and-opensandbox

AWS MCP Security Advisory:
https://github.com/awslabs/mcp/security/advisories/GHSA-4prq-p2x5-w9fh

GitHub Usage Metrics:
https://github.blog/changelog/2026-10-22-custom-skills-agents-and-mcp-servers-in-usage-metrics-api/
https://github.blog/changelog/2026-11-03-byok-and-enterprise-custom-models-in-the-copilot-usage-api/
https://github.blog/changelog/2026-11-05-usage-metrics-api-flags-telemetry-and-version-gaps/

GitHub Enterprise Plugin Controls:
https://github.blog/changelog/2026-10-28-repository-plugin-defaults-and-enterprise-plugin-controls-generally-available/

GitHub Managed MCP:
https://github.blog/changelog/2026-10-28-connect-your-tools-to-copilot-without-all-the-setup/

Plugin4Shell:
https://www.air.security/blog-posts/plugin4shell
```

---

# 75. Final Rule

Do not turn SkillForge into another installer or generic scanner.

The core product moat is:

> **Evidence-backed comparison between declared, resolved, runtime and effective capability.**

Every implementation decision should strengthen that sentence.
