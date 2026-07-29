namespace SkillForge.Domain.Mcp;

/// <summary>
/// One tool as <c>tools/list</c> described it, reduced to the facts a conformance check needs.
/// </summary>
/// <remarks>
/// The JSON layer extracts these; the decision about which of them is a finding is made above it. That split is what
/// lets the conformance rules be tested without a server and without JSON.
/// </remarks>
/// <param name="Name">The tool's name.</param>
/// <param name="HasObjectInputSchema">
/// Whether <c>inputSchema</c> is present and is a JSON object. The specification says it <strong>MUST</strong> be a
/// valid JSON Schema object and not <c>null</c>, so this is the one structural MUST that can be checked from the
/// outside without a schema validator.
/// </param>
/// <param name="DeclaredSchemaDialect">
/// The <c>$schema</c> value, when the schema declares one. Reported, never judged: the specification defaults to
/// 2020-12 when the field is absent and shows an explicit <c>draft-07</c> schema as a valid example, so a dialect other
/// than 2020-12 is not a violation.
/// </param>
/// <param name="HeaderAnnotations">Every <c>x-mcp-header</c> annotation found in the input schema.</param>
public sealed record McpToolSummary(
    string Name,
    bool HasObjectInputSchema,
    string? DeclaredSchemaDialect,
    IReadOnlyList<McpHeaderAnnotation> HeaderAnnotations);
