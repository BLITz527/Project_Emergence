using System.Text;

namespace Emergence.Foundation.Text;

/// <summary>Shared fail-closed UTF-8 policy for durable text.</summary>
public static class StrictUtf8
{
    private static readonly UTF8Encoding Encoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static byte[] GetBytes(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.GetBytes(value);
    }

    public static int GetByteCount(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.GetByteCount(value);
    }

    public static string GetString(ReadOnlySpan<byte> value) => Encoding.GetString(value);

    public static string GetStringWithoutBom(ReadOnlySpan<byte> value)
    {
        if (value.Length >= 3 && value[0] == 0xef && value[1] == 0xbb && value[2] == 0xbf)
        {
            throw new DecoderFallbackException("UTF-8 BOM is not permitted.");
        }

        return Encoding.GetString(value);
    }
}
