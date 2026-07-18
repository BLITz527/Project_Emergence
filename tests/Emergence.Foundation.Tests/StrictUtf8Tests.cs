using System.Text;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Text;

namespace Emergence.Foundation.Tests;

public sealed class StrictUtf8Tests
{
    [Fact]
    public void AsciiAndValidUnicodeRemainExactWithoutBomOrNormalization()
    {
        string value = "Project Emergence · e\u0301 · 😀";
        byte[] bytes = StrictUtf8.GetBytes(value);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(value), bytes);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Equal(value, StrictUtf8.GetStringWithoutBom(bytes));
        Assert.NotEqual(StrictUtf8.GetBytes(value.Normalize()), bytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnpairedSurrogatesFailClosed(bool high)
    {
        string malformed = new([high ? '\ud800' : '\udc00']);
        Assert.Throws<EncoderFallbackException>(() => StrictUtf8.GetBytes(malformed));
        Assert.Throws<EncoderFallbackException>(() => Sha256Digest.ComputeUtf8(malformed));
        using CanonicalHashWriter writer = new();
        Assert.Throws<EncoderFallbackException>(() => writer.WriteString(malformed));
    }

    [Fact]
    public void ValidSurrogatePairIsAcceptedAndBomIsRejected()
    {
        Assert.Equal("😀", StrictUtf8.GetStringWithoutBom(StrictUtf8.GetBytes("😀")));
        Assert.Throws<DecoderFallbackException>(() => StrictUtf8.GetStringWithoutBom([0xef, 0xbb, 0xbf, (byte)'x']));
        Assert.Throws<DecoderFallbackException>(() => StrictUtf8.GetStringWithoutBom([0xff]));
    }
}
