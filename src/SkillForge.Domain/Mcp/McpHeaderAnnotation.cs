namespace SkillForge.Domain.Mcp;

/// <summary>
/// One <c>x-mcp-header</c> annotation: a tool parameter the server wants mirrored into an HTTP header.
/// </summary>
/// <remarks>
/// Worth checking precisely, because the specification's requirements here are unusually strict and land on the
/// <em>client</em>: a Streamable HTTP client <strong>MUST</strong> reject a tool definition whose annotation breaks them,
/// excluding that tool from <c>tools/list</c>. A server shipping one has a tool that simply will not appear, and nothing
/// in its own logs will say so.
/// </remarks>
/// <param name="PropertyName">The schema property carrying the annotation.</param>
/// <param name="HeaderName">The annotation's value — the name portion of the resulting <c>Mcp-Param-{name}</c> header.</param>
/// <param name="PropertyType">
/// The property's declared <c>type</c>, or <see langword="null"/> when it declares none. Only <c>integer</c>,
/// <c>string</c> and <c>boolean</c> are permitted; <c>number</c> is named in the specification as not permitted.
/// </param>
public sealed record McpHeaderAnnotation(string PropertyName, string HeaderName, string? PropertyType);
