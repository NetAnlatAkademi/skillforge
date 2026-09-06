using System.Globalization;
using SkillForge.Application.Abstractions;

namespace SkillForge.Application.Tests.Fakes;

/// <summary>
/// A stand-in hash that is still content-dependent, so equality assertions mean something.
/// </summary>
/// <remarks>
/// FNV-1a rather than SHA-256: the tests care that different bytes produce different values and that the same
/// bytes produce the same one, and the real algorithm lives in Infrastructure where it is tested against known
/// vectors.
/// </remarks>
internal sealed class FakeHashCalculator : IHashCalculator
{
    public string ComputeSha256(ReadOnlySpan<byte> content)
    {
        var hash = 1469598103934665603UL;
        foreach (var b in content)
        {
            hash = (hash ^ b) * 1099511628211UL;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture).PadLeft(64, '0');
    }
}
