using System.Reflection;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Quantities;
using Emergence.Model.Environment;
using Emergence.Presentation.Contracts;
using Emergence.Simulation;
using Emergence.Simulation.Fields;

namespace Emergence.Presentation.Contracts.Tests;

public sealed class EnvironmentPresentationTests
{
    [Fact]
    public void ReferenceSnapshotHasExactDimensionsMaskTotalAndFluidNormalization()
    {
        WorldSession session = EnvironmentSessionFixture.CreatePausedSession();
        FieldChannelId channel = new(ReferenceEnvironmentDefinition.EnergySubstrateId);
        EnvironmentPresentationSnapshot snapshot = new EnvironmentPresentationSnapshotProducer().Create(session, channel);

        Assert.Equal(16U, snapshot.Width);
        Assert.Equal(12U, snapshot.Height);
        Assert.Equal(192, snapshot.NormalizedSurface.Count);
        Assert.Equal(59, snapshot.SolidMask.Count(static value => value));
        Assert.Equal("183686", snapshot.SelectedChannelTotal);
        Assert.Equal(new MatterAmount(1056), snapshot.MinimumFluidAmount);
        Assert.Equal(new MatterAmount(1708), snapshot.MaximumFluidAmount);
        RegionLatticeDefinition region = ReferenceEnvironmentDefinition.Create().Regions.Single();
        Assert.Equal(0d, snapshot.NormalizedSurface[region.GetLinearIndex(new(1, 1))]);
        Assert.Equal(1d, snapshot.NormalizedSurface[region.GetLinearIndex(new(14, 10))]);
        int solid = region.GetLinearIndex(new(8, 5));
        Assert.True(snapshot.SolidMask[solid]);
        Assert.Equal(0d, snapshot.NormalizedSurface[solid]);
        Assert.False(snapshot.HasBiologicalState);
    }

    [Fact]
    public void SnapshotAndProbeDoNotMutateTickSequencesRngOrEnvironment()
    {
        WorldSession session = EnvironmentSessionFixture.CreatePausedSession();
        string state = session.StateDigest.ToString();
        string environment = session.EnvironmentState!.Digest.ToString();
        var tick = session.CurrentTick;
        var command = session.LastCommandSequence;
        var worldEvent = session.LastEventSequence;
        EnvironmentPresentationSnapshotProducer producer = new();

        _ = producer.Create(session, new(ReferenceEnvironmentDefinition.WasteId));
        FieldProbePresentation probe = producer.Probe(session, new(8, 5), new(ReferenceEnvironmentDefinition.EnergySubstrateId));

        Assert.True(probe.IsSolid);
        Assert.Equal(new MatterAmount(0), probe.RawAmount);
        Assert.Equal(new VolumeAmount(0), probe.EffectiveVolume);
        Assert.Equal("unavailable (zero effective volume)", probe.DerivedConcentrationDisplay);
        Assert.Equal("AUTHORITATIVE CELL SAMPLE", probe.SampleKind);
        Assert.Equal(state, session.StateDigest.ToString());
        Assert.Equal(environment, session.EnvironmentState!.Digest.ToString());
        Assert.Equal(tick, session.CurrentTick);
        Assert.Equal(command, session.LastCommandSequence);
        Assert.Equal(worldEvent, session.LastEventSequence);
    }

    [Fact]
    public void DtoDefensivelyCopiesSurfacesAndHasNoGodotOrWritableArraySurface()
    {
        double[] values = [0, 1];
        bool[] mask = [false, true];
        EnvironmentPresentationSnapshot snapshot = new(
            ReferenceEnvironmentDefinition.RegionId, default, default, default, 2, 1,
            new(ReferenceEnvironmentDefinition.EnergySubstrateId), "1", values, mask, new(1), new(1));
        values[0] = 0.75;
        mask[0] = true;
        Assert.Equal(0d, snapshot.NormalizedSurface[0]);
        Assert.False(snapshot.SolidMask[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)snapshot.NormalizedSurface)[0] = 1);
        Assert.DoesNotContain(typeof(EnvironmentPresentationSnapshot).Assembly.GetReferencedAssemblies(), static assembly => assembly.Name?.StartsWith("Godot", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(typeof(EnvironmentPresentationSnapshot).GetProperties(), static property => property.PropertyType.IsArray || property.SetMethod?.IsPublic == true);
        Assert.DoesNotContain(typeof(EnvironmentPresentationSnapshot).GetMethods(BindingFlags.Public | BindingFlags.Instance), static method => method.ReturnType.IsArray);
    }
}
