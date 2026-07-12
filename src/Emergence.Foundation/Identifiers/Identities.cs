using System.Text.Json.Serialization;

namespace Emergence.Foundation.Identifiers;

public sealed record WorldIdentity
{
    [JsonConstructor]
    public WorldIdentity(WorldId worldId)
    {
        if (worldId.IsEmpty) throw new ArgumentException("World identity cannot be empty.", nameof(worldId));
        WorldId = worldId;
    }

    [JsonPropertyOrder(0)] public WorldId WorldId { get; }
}

public sealed record BranchIdentity
{
    [JsonConstructor]
    public BranchIdentity(WorldId worldId, BranchId branchId)
    {
        if (worldId.IsEmpty) throw new ArgumentException("World identity cannot be empty.", nameof(worldId));
        if (branchId.IsEmpty) throw new ArgumentException("Branch identity cannot be empty.", nameof(branchId));
        WorldId = worldId;
        BranchId = branchId;
    }

    [JsonPropertyOrder(0)] public WorldId WorldId { get; }
    [JsonPropertyOrder(1)] public BranchId BranchId { get; }
}
