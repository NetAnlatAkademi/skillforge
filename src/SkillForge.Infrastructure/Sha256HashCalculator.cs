using System.Security.Cryptography;
using SkillForge.Application.Abstractions;

namespace SkillForge.Infrastructure;

/// <summary>
/// Computes SHA-256 hashes.
/// </summary>
public sealed class Sha256HashCalculator : IHashCalculator
{
    /// <inheritdoc />
    public string ComputeSha256(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);

        return Convert.ToHexStringLower(hash);
    }
}
