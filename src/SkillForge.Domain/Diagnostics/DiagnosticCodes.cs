namespace SkillForge.Domain.Diagnostics;

/// <summary>
/// The complete set of diagnostic codes SkillForge can emit.
/// </summary>
/// <remarks>
/// Codes are part of the public contract: CI configurations and suppression files refer to them, so a
/// released code is never renumbered, reused for a different rule, or removed. Every code listed here
/// is documented in <c>docs/validation-rules.md</c>.
/// </remarks>
public static class DiagnosticCodes
{
    /// <summary>
    /// <c>SKILL.md</c> was not found, or exists but could not be read. Both read the same way to the
    /// person running the CLI: SkillForge could not get at the skill.
    /// </summary>
    public const string SkillFileNotFound = "SF0001";

    /// <summary>The YAML frontmatter block was not found.</summary>
    public const string FrontmatterNotFound = "SF0002";

    /// <summary>The YAML frontmatter could not be parsed.</summary>
    public const string FrontmatterNotParsable = "SF0003";

    /// <summary>The <c>name</c> field is missing.</summary>
    public const string NameMissing = "SF0004";

    /// <summary>The <c>description</c> field is missing.</summary>
    public const string DescriptionMissing = "SF0005";

    /// <summary>The skill name is not a valid identifier.</summary>
    public const string NameInvalid = "SF0006";

    /// <summary>A file referenced by the skill does not exist.</summary>
    public const string ReferencedFileNotFound = "SF0007";

    /// <summary>A path escapes the skill directory.</summary>
    public const string PathEscapesSkillDirectory = "SF0008";

    /// <summary>The same metadata field is declared more than once.</summary>
    public const string DuplicateMetadataField = "SF0009";

    /// <summary>The package version is not a valid version string.</summary>
    public const string PackageVersionInvalid = "SF0010";

    /// <summary>The description is too short to be useful.</summary>
    public const string DescriptionTooShort = "SF1001";

    /// <summary>The description does not state when the skill should activate.</summary>
    public const string DescriptionWithoutActivationContext = "SF1002";

    /// <summary><c>SKILL.md</c> is longer than the recommended length.</summary>
    public const string SkillFileTooLong = "SF1003";

    /// <summary>A file in the skill directory is never referenced.</summary>
    public const string UnusedFile = "SF1004";

    /// <summary>The skill references an external URL.</summary>
    public const string ExternalUrlPresent = "SF1005";

    /// <summary>The skill ships a script but declares no permission for it.</summary>
    public const string ScriptWithoutDeclaredPermission = "SF1006";

    /// <summary>A shell command requests broad privileges.</summary>
    public const string BroadShellPrivileges = "SF1007";

    /// <summary>Package dependencies are not pinned to specific versions.</summary>
    public const string UnpinnedDependencies = "SF1008";

    /// <summary>No license is declared.</summary>
    public const string LicenseMissing = "SF1009";

    /// <summary>No agent compatibility information is declared.</summary>
    public const string CompatibilityMissing = "SF1010";

    /// <summary>
    /// A reference leaves the skill directory but points at a sibling — normal inside a collection of skills,
    /// but it cannot be satisfied by the skill on its own.
    /// </summary>
    public const string ReferenceLeavesSkill = "SF1011";

    /// <summary>
    /// <c>skillforge.yaml</c> exists but could not be parsed, so its settings were ignored.
    /// </summary>
    public const string ConfigurationNotParsable = "SF1012";

    /// <summary>
    /// A <c>version</c> field was found at the top level of the frontmatter, where the schema does not look for
    /// it. SkillForge reads it anyway rather than losing it silently, and says so.
    /// </summary>
    public const string VersionOutsideMetadata = "SF1013";

    /// <summary>
    /// A file under <c>evals</c> could not be read or parsed, so its cases were skipped. Reported rather than
    /// fatal, for the same reason as SF1012: the rest of the suite is still worth running.
    /// </summary>
    public const string EvalFileNotParsable = "SF1014";

    /// <summary>
    /// A provider's own configuration file was found but could not be read or parsed, so what it declares is
    /// missing from the migration inventory. Same shape as SF1012 and SF1014: reported rather than fatal, because
    /// the rest of the inventory is still worth having and a silent gap would look like an empty configuration.
    /// </summary>
    public const string ProviderConfigurationNotParsable = "SF1015";

    /// <summary>
    /// A remote registry could not be searched, or answered with something that could not be read: unreachable,
    /// too slow, larger than the response limit, not JSON, or more results than one run reads. Same shape as
    /// SF1012, SF1014 and SF1015 — reported rather than fatal, because a registry that will not answer is a fact
    /// about that registry and not a reason to abandon the run.
    /// </summary>
    public const string DiscoveryResponseNotUsable = "SF1016";

    /// <summary>The skill contains a script.</summary>
    public const string ContainsScript = "SF2001";

    /// <summary>The skill contains an external URL.</summary>
    public const string ContainsExternalUrl = "SF2002";

    /// <summary>The skill contains a binary file.</summary>
    public const string ContainsBinaryFile = "SF2003";

    /// <summary>The skill contains an <c>evals</c> folder.</summary>
    public const string ContainsEvals = "SF2004";

    /// <summary>
    /// The description claims the skill applies always, or to everything — an activation scope so broad that an
    /// agent has nothing to match it against.
    /// </summary>
    public const string ActivationTooBroad = "SF3001";

    /// <summary>
    /// The skill's text pushes an agent to prefer it over its other instructions rather than describing when it
    /// applies.
    /// </summary>
    public const string ActivationManipulation = "SF3002";

    /// <summary>
    /// The body's prose tells the agent to set aside or override instructions it was given — the shape prompt
    /// injection takes when it arrives inside a skill rather than inside user input.
    /// </summary>
    public const string BodyInstructionOverride = "SF4001";

    /// <summary>
    /// The body's prose tells the agent to keep something from the person it is working for.
    /// </summary>
    public const string BodyConcealmentInstruction = "SF4002";

    /// <summary>
    /// The skill fetches something remote from a reference that can change — a branch, a "latest" tag, a
    /// latest-release URL — so running it twice is not guaranteed to run the same thing twice.
    /// </summary>
    public const string MutableRemoteReference = "SF5001";

    /// <summary>
    /// Nothing observed says where an asset came from. Reported by <c>provenance diff</c> about an asset that
    /// arrived, or one whose origin stopped being determinable.
    /// </summary>
    /// <remarks>
    /// <c>SF5001</c> is a rule about a skill's own references and predates this block. <c>SF5101</c> onwards are
    /// about distribution: where an asset came from, who publishes it, and how it gets its next version. Both are
    /// SF5xxx; the gap between them is what lets a reader tell the two apart in one report.
    /// </remarks>
    public const string ProvenanceUnknown = "SF5101";

    /// <summary>
    /// An asset declares an upstream it is not distributed from — a fork, in the asset's own words. Information
    /// rather than a warning: forking is ordinary, and only the reviewer knows whether this one was intended.
    /// </summary>
    public const string ProvenanceForked = "SF5102";

    /// <summary>
    /// An asset's files differ from the revision it names, so the commit in its provenance describes something
    /// other than what is on disk.
    /// </summary>
    public const string ProvenanceLocallyModified = "SF5103";

    /// <summary>The marketplace an asset is distributed through is not the one recorded earlier.</summary>
    public const string DistributionMarketplaceChanged = "SF5201";

    /// <summary>
    /// An asset that was not updating itself now does. What arrives next will not pass through whoever reviewed
    /// this change.
    /// </summary>
    public const string DistributionAutomaticUpdate = "SF5301";

    /// <summary>An asset that named an immutable revision now names one that can move.</summary>
    public const string DistributionVersionFloating = "SF5302";

    /// <summary>
    /// An update that arrives without review added a capability: a script, a host, a permission, an MCP server.
    /// Reported by <c>update analyze</c>, which is the only command that can see both halves.
    /// </summary>
    public const string DistributionUpdateAddedCapability = "SF5303";

    /// <summary>
    /// The publisher changed between two revisions of the same asset. An error: whoever the organisation decided
    /// to trust is not who is shipping this now.
    /// </summary>
    public const string DistributionPublisherChanged = "SF5401";

    /// <summary>
    /// An asset's content fingerprint changed while its declared version did not, so a consumer pinned to that
    /// version received different bytes without being told.
    /// </summary>
    public const string DistributionHashChanged = "SF5501";

    /// <summary>
    /// The skill's reach grew while its declared version stayed the same, so a consumer pinned to that version
    /// received the change without being told. Reported by <c>diff</c>, which is the only command that can see it.
    /// </summary>
    public const string VersionSilentAboutGrowth = "SF6001";

    /// <summary>
    /// The later revision declares a permission the earlier one did not. Reported by <c>diff</c>, which is the only
    /// command that can see it.
    /// </summary>
    public const string PermissionAdded = "SF6002";

    /// <summary>The later revision ships a script the earlier one did not.</summary>
    public const string ScriptAdded = "SF6003";

    /// <summary>The later revision points at a host the earlier one did not.</summary>
    public const string ExternalDomainAdded = "SF6004";

    /// <summary>
    /// The later revision gave something up: a permission, a script or a host the earlier one had. Information
    /// rather than a warning — a skill that reaches less far than it did is not a risk, but a consumer relying on
    /// what was removed still needs to see it.
    /// </summary>
    public const string ReachNarrowed = "SF6005";

    /// <summary>
    /// The skill declares compatibility with a provider SkillForge does not recognise, so nothing was checked
    /// against it. Usually a spelling of a known identifier; sometimes a provider SkillForge has not learned yet.
    /// </summary>
    public const string ProviderUnknown = "SF7001";

    /// <summary>
    /// The <c>name</c> is longer than a provider the skill declares compatibility with accepts.
    /// </summary>
    public const string ProviderNameTooLong = "SF7002";

    /// <summary>
    /// The <c>description</c> is longer than a provider the skill declares compatibility with accepts.
    /// </summary>
    public const string ProviderDescriptionTooLong = "SF7003";

    /// <summary>
    /// The kind of identity an MCP server is reached with changed — a person's OAuth session became a workload
    /// identity, or the other way round.
    /// </summary>
    /// <remarks>
    /// <c>SF7001</c>-<c>SF7003</c> are about agent providers and predate this block. <c>SF7101</c> onwards are
    /// about the identity an agent connects with, and <c>SF7301</c> onwards about the tool surface a server opens
    /// to it. Same band, three blocks; the gaps are what let a reader tell them apart.
    /// </remarks>
    public const string IdentityTypeChanged = "SF7101";

    /// <summary>
    /// A credential that does not expire on its own replaced one that did, so revoking access became a deliberate
    /// act rather than a matter of waiting.
    /// </summary>
    public const string IdentityBecameLongLived = "SF7102";

    /// <summary>An identity asks for scopes it did not ask for before.</summary>
    public const string DelegationScopeExpanded = "SF7201";

    /// <summary>An identity that acted for itself now acts on behalf of another party.</summary>
    public const string DelegationEnabled = "SF7202";

    /// <summary>
    /// An MCP server exposes more tools than the configured threshold, so every one of them is in the agent's
    /// context whether or not the task needs it.
    /// </summary>
    public const string McpToolSurfaceLarge = "SF7301";

    /// <summary>
    /// A server exposes tools that change or delete things, or that reach credentials, and does so from the first
    /// response — before anything has decided the task needs them.
    /// </summary>
    public const string McpPrivilegedToolsExposed = "SF7302";

    /// <summary>
    /// Nothing observed says the server narrows what it exposes as a task goes on, so the whole tool surface is in
    /// play from the start. Information rather than a warning: progressive discovery is new, and most servers do
    /// not implement it yet.
    /// </summary>
    public const string McpNoProgressiveDiscovery = "SF7401";

    /// <summary>
    /// An MCP server is declared over the HTTP+SSE transport, which the specification deprecated in
    /// <c>2025-03-26</c> in favour of Streamable HTTP.
    /// </summary>
    public const string McpDeprecatedTransport = "SF8001";

    /// <summary>
    /// An MCP server is declared at a plaintext <c>http://</c> URL on a host that is not loopback, so whatever
    /// authorises the connection crosses the network in the clear.
    /// </summary>
    public const string McpPlaintextEndpoint = "SF8002";

    /// <summary>
    /// An MCP server's command resolves a package at launch without pinning a version, so two launches are not
    /// guaranteed to run the same code. The MCP-declaration counterpart of SF5001.
    /// </summary>
    public const string McpServerCommandNotPinned = "SF8003";

    /// <summary>
    /// A probed MCP server does not implement <c>server/discover</c>, which <c>2026-07-28</c> made mandatory — so it
    /// is almost certainly a handshake-based revision (<c>2025-11-25</c> or earlier).
    /// </summary>
    public const string McpNoDiscovery = "SF8004";

    /// <summary>
    /// A probed MCP server declares a capability the specification has deprecated, such as <c>logging</c>.
    /// </summary>
    public const string McpDeprecatedCapability = "SF8005";

    /// <summary>
    /// A server requires authorization but its challenge does not name its Protected Resource Metadata, which MCP
    /// servers must implement and clients must use to find the authorization server.
    /// </summary>
    public const string McpAuthorizationWithoutMetadata = "SF8006";

    /// <summary>
    /// A tool's <c>inputSchema</c> is absent or is not a JSON object, where the specification requires a valid JSON
    /// Schema object.
    /// </summary>
    public const string McpToolInputSchemaInvalid = "SF8007";

    /// <summary>
    /// A tool carries an <c>x-mcp-header</c> annotation that breaks the constraints a Streamable HTTP client must reject
    /// the whole tool over, so the tool would silently disappear from that client's tool list.
    /// </summary>
    public const string McpToolHeaderAnnotationInvalid = "SF8008";

    /// <summary>
    /// A tool's name falls outside the specification's naming guidance on length, characters or uniqueness.
    /// </summary>
    public const string McpToolNameOutsideGuidance = "SF8009";

    /// <summary>
    /// A server's <c>tools/list</c> was not read to the end, so its tool count is a floor rather than a total.
    /// Either SkillForge stopped at its page limit or the server repeated a pagination cursor.
    /// </summary>
    public const string McpToolListIncomplete = "SF8010";

    /// <summary>
    /// An MCP server the organisation's policy does not permit is declared: a deny rule names it, or no allow rule
    /// does and the policy denies by default.
    /// </summary>
    /// <remarks>
    /// <c>SF8001</c>–<c>SF8009</c> are protocol and tool conformance, reported by <c>mcp inspect</c> and
    /// <c>migrate inspect</c>. <c>SF8101</c> onwards are policy enforcement, reported by <c>policy check</c> and
    /// <c>policy diff</c>. Both blocks are MCP, which is why they share a band; the numeric gap is what lets a
    /// reader tell a fact about a server from a decision about one in the same report.
    /// </remarks>
    public const string McpServerBlockedByPolicy = "SF8101";

    /// <summary>
    /// A policy rule now matches everything an earlier rule matched, and more — the <c>api.company.com</c> to
    /// <c>*.company.com</c> edit, which is one character in a patch and a change of scope in effect.
    /// </summary>
    public const string McpPolicyWildcardExpanded = "SF8102";

    /// <summary>
    /// An entry in the policy's <c>mcp</c> section could not be interpreted, so it was not applied. An error rather
    /// than a warning: a rule that failed to load is a rule that is not protecting anything.
    /// </summary>
    public const string McpPolicyNotParsable = "SF8103";

    /// <summary>
    /// The policy governs which MCP servers may be connected to but does not deny by default, so a server no rule
    /// names is either permitted or undecided.
    /// </summary>
    public const string McpPolicyFailOpen = "SF8104";

    /// <summary>
    /// A server is permitted only because a rule matched the name its configuration gives it. The name is chosen by
    /// the file under review, so on its own it identifies nothing.
    /// </summary>
    public const string McpPolicyMatchedByNameOnly = "SF8105";

    /// <summary>
    /// The later policy permits a local MCP command the earlier one did not — by adding an allow rule, or by
    /// removing a deny rule.
    /// </summary>
    public const string McpPolicyLocalCommandPermitted = "SF8106";

    /// <summary>
    /// The later policy permits a remote MCP endpoint the earlier one did not — by adding an allow rule, or by
    /// removing a deny rule.
    /// </summary>
    public const string McpPolicyRemoteDomainPermitted = "SF8107";

    /// <summary>
    /// A discovered MCP server answered with a tool the registry that listed it did not declare — a capability
    /// that is running and was never reviewed. Reported by <c>discovery verify</c>.
    /// </summary>
    public const string DiscoveryUnexpectedRuntimeTool = "SF8201";

    /// <summary>
    /// A registry declares a tool that the server it lists did not answer with. Usually a stale listing, and
    /// worth knowing because a skill written against the listing will call something that is not there.
    /// </summary>
    public const string DiscoveryDeclaredToolMissing = "SF8202";

    /// <summary>
    /// A discovered server's tool count could not be compared name by name — most often because the listing
    /// declares no capabilities at all — and the two counts differ.
    /// </summary>
    public const string DiscoveryToolCountDrift = "SF8203";

    /// <summary>
    /// The version a registry names and the version a server reports about itself disagree. Two claims, neither
    /// verified by the protocol.
    /// </summary>
    public const string DiscoverySelfReportedVersionDrift = "SF8204";

    /// <summary>
    /// The policy file exists but could not be read or parsed, so no policy was applied. An error rather than a
    /// warning, unlike SF1012: a run that was asked to check policies and checked none has not done its job, and a
    /// build that passes because the rules failed to load is the worst possible outcome.
    /// </summary>
    public const string PolicyNotParsable = "SF9001";

    /// <summary>The skill can run shell commands and the organisation's policy does not allow it.</summary>
    public const string PolicyShellForbidden = "SF9002";

    /// <summary>The skill can write to the file system and the organisation's policy does not allow it.</summary>
    public const string PolicyFilesystemWriteForbidden = "SF9003";

    /// <summary>The skill points at a host the organisation's policy does not list.</summary>
    public const string PolicyDomainNotAllowed = "SF9004";

    /// <summary>
    /// The organisation's policy requires a skill's origin to be identifiable and it is not.
    /// </summary>
    public const string PolicyProvenanceMissing = "SF9005";

    /// <summary>The organisation's policy requires a license and the skill declares none.</summary>
    public const string PolicyLicenseMissing = "SF9006";

    /// <summary><c>SKILL.md</c> is longer than the organisation's policy allows.</summary>
    public const string PolicySkillFileTooLong = "SF9007";

    /// <summary>
    /// A suppression in the policy file gives no reason, so it was not applied. A policy that can silence a rule
    /// without saying why is a policy that stops recording decisions.
    /// </summary>
    public const string PolicySuppressionWithoutReason = "SF9008";

    /// <summary>
    /// A policy rule was read but not evaluated, because this command cannot observe what it asks about. Reported
    /// so a rule that never runs cannot be mistaken for a rule that passed.
    /// </summary>
    public const string PolicyRuleNotEvaluated = "SF9009";

    /// <summary>
    /// A policy rule outside the <c>mcp</c> section was relaxed between two snapshots. Reported by
    /// <c>policy diff</c>, which is the only command that can see it.
    /// </summary>
    /// <remarks>
    /// One code for every non-MCP relaxation rather than one per rule. Each finding names the rule and both values,
    /// so nothing is lost; what would be gained by a code per rule is the ability to suppress them separately, and
    /// no one has asked for that. The MCP relaxations have their own codes because the ecosystem review that asked
    /// for this command asked for those by number.
    /// </remarks>
    public const string PolicyRelaxed = "SF9010";
}
