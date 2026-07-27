namespace SkillForge.Application.Abstractions;

/// <summary>
/// Computes content hashes.
/// </summary>
public interface IHashCalculator
{
    /// <summary>Computes a SHA-256 hash.</summary>
    /// <param name="content">Bytes to hash.</param>
    /// <returns>The hash as lowercase hexadecimal.</returns>
    string ComputeSha256(ReadOnlySpan<byte> content);
}
