namespace LSUtils.Terrain.Navigation;

using System;

/// <summary>
/// Describes how a particular mover traverses a terrain world.
/// A terrain cost of zero or less marks that terrain as impassable.
/// </summary>
public sealed class TerrainNavigationSettings<TTerrainType, TContentType> {
    public Func<TerrainPatch<TTerrainType>?, float> GetTerrainCost { get; }
    public Func<TerrainContent<TContentType>, bool> BlocksContent { get; }
    public float AgentRadius { get; }
    public float MinimumCost { get; }

    public TerrainNavigationSettings(
        Func<TerrainPatch<TTerrainType>?, float> getTerrainCost,
        Func<TerrainContent<TContentType>, bool>? blocksContent = null,
        float agentRadius = 0f,
        float minimumCost = 1f) {
        GetTerrainCost = getTerrainCost ?? throw new LSArgumentNullException(nameof(getTerrainCost));
        BlocksContent = blocksContent ?? (_ => true);
        AgentRadius = MathF.Max(0f, agentRadius);
        MinimumCost = MathF.Max(float.Epsilon, minimumCost);
    }
}
