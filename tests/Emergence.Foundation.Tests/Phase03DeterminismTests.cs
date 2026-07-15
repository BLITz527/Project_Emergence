using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Tests;

public sealed class Phase03DeterminismTests
{
    private static readonly RngSeed256 Seed = RngSeed256.Parse(FoundationRngSelfTest.Seed);
    private static readonly RngScopeKey Scope = new(new StableId128(0x0123456789abcdef, 0xfedcba9876543210));
    private static readonly RngSampleAddress Address = new(new("foundation.self-test"), Scope, 42);
    private static readonly DeterministicAddressedRng Rng = new(Seed, RngDomainCatalog.Phase03);

    [Theory]
    [InlineData("a")]
    [InlineData("a-b.c0")]
    [InlineData("foundation.self-test")]
    public void DomainAcceptsCanonicalLexemes(string text) => Assert.Equal(text, new RngDomainId(text).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("a..b")]
    [InlineData("a_b")]
    [InlineData("1a")]
    [InlineData(" a")]
    [InlineData("a ")]
    [InlineData("a.")]
    public void DomainRejectsNoncanonicalLexemes(string text) => Assert.False(RngDomainId.TryParse(text, out _));

    [Fact]
    public void DomainLengthBoundariesAreEnforced()
    {
        Assert.True(RngDomainId.TryParse(string.Join('.', Enumerable.Repeat(new string('a', 31), 3)), out _));
        Assert.False(RngDomainId.TryParse(new string('a', 33), out _)); Assert.False(RngDomainId.TryParse(new string('a', 97), out _));
    }

    [Fact]
    public void ScopeAndAddressRejectDefaultsAndOrderDeterministically()
    {
        Assert.Throws<ArgumentException>(() => new RngScopeKey(default)); Assert.Throws<ArgumentException>(() => new RngSampleAddress(default, Scope, 0)); Assert.Throws<ArgumentException>(() => new RngSampleAddress(new("foundation.self-test"), default, 0));
        Assert.True(new RngSampleAddress(new("foundation.reference"), Scope, 99).CompareTo(Address) < 0); Assert.True(Address.CompareTo(new(Address.Domain, Scope, 43)) < 0);
        string noncanonical = JsonDefaults.Serialize(Address, false).Replace("\"42\"", "\"+42\"", StringComparison.Ordinal); Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RngSampleAddress>(noncanonical, JsonDefaults.Compact));
    }

    [Fact]
    public void RngBlockLanesAreIndependentlyLittleEndian()
    {
        RngBlock256 block = Rng.GenerateBlock(Address); byte[] bytes = Convert.FromHexString(block.ToString());
        for (int lane = 0; lane < 4; lane++) Assert.Equal(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(lane * 8, 8)), block.GetLane(lane));
        Assert.Throws<ArgumentOutOfRangeException>(() => block.GetLane(-1)); Assert.Throws<ArgumentOutOfRangeException>(() => block.GetLane(4));
    }

    [Fact]
    public void AddressedSamplingIsRepeatableOrderIndependentAndSeparated()
    {
        string primary = Rng.GenerateBlock(Address).ToString();
        _ = Rng.GenerateBlock(new(new("foundation.reference"), Scope, 900)); _ = Rng.GenerateBlock(new(Address.Domain, Scope, 41));
        Assert.Equal(primary, Rng.GenerateBlock(Address).ToString());
        Assert.NotEqual(primary, new DeterministicAddressedRng(RngSeed256.Parse(new string('0', 64)), RngDomainCatalog.Phase03).GenerateBlock(Address).ToString());
        Assert.NotEqual(primary, Rng.GenerateBlock(new(Address.Domain, new(new StableId128(1, 2)), 42)).ToString());
        Assert.NotEqual(primary, Rng.GenerateBlock(new(Address.Domain, Scope, 43)).ToString()); Assert.NotEqual(primary, Rng.GenerateBlock(Address, 1).ToString());
    }

    [Fact]
    public void ProductionEncodingMatchesIndependentReferenceAcrossIndices()
    {
        foreach (UInt128 index in new UInt128[] { 0, 1, 42, ulong.MaxValue, UInt128.MaxValue })
        {
            RngSampleAddress address = new(Address.Domain, Scope, index); byte[] encoded = IndependentEncoding(Seed, address, 7);
            Assert.Equal(Convert.ToHexStringLower(encoded), Convert.ToHexStringLower(Rng.GetCanonicalEncoding(address, 7)));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(encoded)), Rng.GenerateBlock(address, 7).ToString());
        }
    }

    [Fact]
    public void CultureCannotChangeCanonicalRngOutput()
    {
        CultureInfo prior = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); Assert.Equal(FoundationRngSelfTest.ExpectedBlock, Rng.GenerateBlock(Address).ToString()); CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Assert.Equal(FoundationRngSelfTest.ExpectedBlock, Rng.GenerateBlock(Address).ToString()); } finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void HotBlockGenerationAllocatesNoManagedBytesAfterWarmup()
    {
        for (int i = 0; i < 100; i++) _ = Rng.GenerateBlock(Address);
        long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) _ = Rng.GenerateBlock(Address); long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, after - before);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(10UL)]
    [InlineData(uint.MaxValue)]
    [InlineData(ulong.MaxValue)]
    public void BoundedSamplesStayBelowBound(ulong bound)
    {
        for (UInt128 index = 0; index < 32; index++) Assert.True(Rng.SampleUInt64Below(new(Address.Domain, Scope, index), bound) < bound);
    }

    [Fact]
    public void BoundedSamplingExercisesRejectionAndAttemptOverflow()
    {
        Assert.Equal(7UL, DeterministicAddressedRng.SampleUInt64BelowCore(new FixtureCandidates([0, 17]), 10, 0));
        Assert.Throws<OverflowException>(() => DeterministicAddressedRng.SampleUInt64BelowCore(new FixtureCandidates([0]), 10, ulong.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rng.SampleUInt64Below(Address, 0)); Assert.Equal(0UL, Rng.SampleUInt64Below(Address, 1));
    }

    [Fact]
    public void DomainCatalogPermutationRoundTripAndDigestValidationAreFailClosed()
    {
        RngDomainCatalog reversed = new(RngDomainCatalog.Phase03.Entries.Reverse()); Assert.Equal(RngDomainCatalog.Phase03.Digest, reversed.Digest); Assert.Equal(RngDomainCatalog.Phase03, JsonSerializer.Deserialize<RngDomainCatalog>(JsonDefaults.Serialize(RngDomainCatalog.Phase03), JsonDefaults.Compact));
        string bad = JsonDefaults.Serialize(RngDomainCatalog.Phase03).Replace(RngDomainCatalog.Phase03.Digest.ToString(), new string('0', 64), StringComparison.Ordinal); Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RngDomainCatalog>(bad, JsonDefaults.Compact));
    }

    [Fact]
    public void Phase03AlgorithmEntriesAreExactAndUnique()
    {
        string[] added = ["foundation.rng-seed", "foundation.rng-addressed-sha256", "foundation.rng-bounded-uint64", "foundation.rng-domain-catalog", "foundation.ruleset-manifest", "foundation.ruleset-registry"];
        Assert.Equal(11, AlgorithmCatalog.Phase03.Entries.Count); Assert.Equal(11, AlgorithmCatalog.Phase03.Entries.Select(x => x.Id).Distinct().Count()); Assert.All(added, id => Assert.Single(AlgorithmCatalog.Phase03.Entries, x => x.Id.ToString() == id));
    }

    [Fact]
    public void RulesetKeysAreExactAndRejectEmptyIdentifiers()
    {
        RulesetKey key = new(RulesetId.FromUInt64(1), new(1, 2, 3)); Assert.Equal("00000000000000000000000000000001@1.2.3", key.ToString()); Assert.Equal(key, RulesetKey.Parse(key.ToString())); Assert.False(RulesetKey.TryParse("latest", out _)); Assert.Throws<ArgumentException>(() => new RulesetKey(default, default));
    }

    [Fact]
    public void DescriptorDisplayAndFormatValidationAreStrict()
    {
        RulesetDescriptor reference = FoundationReferenceRuleset.Create();
        Assert.Throws<ArgumentException>(() => new RulesetDescriptor(new(2, 0, 0), reference.Key, reference.DisplayName, reference.Algorithms, reference.RngDomains, reference.Configuration));
        foreach (string invalid in new[] { "", " leading", "trailing ", "control\u0001" }) Assert.Throws<ArgumentException>(() => new RulesetDescriptor(new(1, 0, 0), reference.Key, invalid, reference.Algorithms, reference.RngDomains, reference.Configuration));
        Assert.Throws<ArgumentException>(() => new RulesetDescriptor(new(1, 0, 0), reference.Key, "invalid\ud800", reference.Algorithms, reference.RngDomains, reference.Configuration));
        Assert.NotNull(new RulesetDescriptor(new(1, 0, 0), reference.Key, string.Concat(Enumerable.Repeat("😀", 120)), reference.Algorithms, reference.RngDomains, reference.Configuration));
        Assert.Throws<ArgumentException>(() => new RulesetDescriptor(new(1, 0, 0), reference.Key, string.Concat(Enumerable.Repeat("😀", 121)), reference.Algorithms, reference.RngDomains, reference.Configuration));
    }

    [Fact]
    public void RegistryIsOrderIndependentExactAndFailClosed()
    {
        RulesetDescriptor first = FoundationReferenceRuleset.Create(); RulesetDescriptor second = Descriptor(2);
        RulesetRegistry registry = new([second, first]); Assert.Equal(first.Key, registry.Entries[0].Key); Assert.Equal(registry.Digest, new RulesetRegistry([first, second]).Digest); Assert.True(registry.TryGet(second.Key, out RulesetDescriptor? found)); Assert.Equal(second, found); Assert.False(registry.TryGet(new(RulesetId.FromUInt64(2), new(1, 0, 1)), out _));
        Assert.Throws<ArgumentException>(() => new RulesetRegistry([first, first])); RulesetDescriptor[] source = [first]; RulesetRegistry immutable = new(source); source[0] = second; Assert.Equal(first, immutable.Entries[0]);
        string bad = JsonDefaults.Serialize(immutable).Replace(immutable.Digest.ToString(), new string('0', 64), StringComparison.Ordinal); Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RulesetRegistry>(bad, JsonDefaults.Compact));
    }

    private static RulesetDescriptor Descriptor(ulong id) { RulesetDescriptor value = FoundationReferenceRuleset.Create(); return new(new(1, 0, 0), new(RulesetId.FromUInt64(id), new(1, 0, 0)), value.DisplayName, value.Algorithms, value.RngDomains, value.Configuration); }

    private static byte[] IndependentEncoding(RngSeed256 seed, RngSampleAddress address, ulong attempt)
    {
        using MemoryStream stream = new(); stream.Write(Encoding.ASCII.GetBytes("PE-CANONICAL/1\0")); WriteString(stream, "ProjectEmergence.RngBlock.v1"); byte[] seedBytes = seed.ToByteArray(); stream.WriteByte(2); WriteU64(stream, 32); stream.Write(seedBytes); WriteString(stream, address.Domain.ToString()); stream.WriteByte(3); WriteU64(stream, address.Scope.Value.High); stream.WriteByte(3); WriteU64(stream, address.Scope.Value.Low); stream.WriteByte(4); WriteU64(stream, (ulong)address.SampleIndex); WriteU64(stream, (ulong)(address.SampleIndex >> 64)); stream.WriteByte(3); WriteU64(stream, attempt); return stream.ToArray();
    }
    private static void WriteString(Stream stream, string value) { byte[] bytes = Encoding.UTF8.GetBytes(value); stream.WriteByte(1); WriteU64(stream, (ulong)bytes.Length); stream.Write(bytes); }
    private static void WriteU64(Stream stream, ulong value) { Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); stream.Write(bytes); }
    private readonly struct FixtureCandidates(ulong[] values) : IRngCandidateSource { public ulong Candidate(ulong attempt) => values.Length == 1 ? values[0] : values[(int)attempt]; }
}
