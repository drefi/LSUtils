namespace LSUtils.Terrain.Navigation;

using System;

/// <summary>Measurements collected while a navigation mesh topology is built.</summary>
public sealed class TerrainNavigationBuildStatistics {
    internal TerrainNavigationBuildStatistics(int obstacleCount, int nodeCount, int edgeCount, long visibilityTests, long visibleConnections, long obstacleCandidateChecks, long terrainCostSamples, TimeSpan elapsed) {
        ObstacleCount = obstacleCount;
        NodeCount = nodeCount;
        EdgeCount = edgeCount;
        VisibilityTests = visibilityTests;
        VisibleConnections = visibleConnections;
        ObstacleCandidateChecks = obstacleCandidateChecks;
        TerrainCostSamples = terrainCostSamples;
        Elapsed = elapsed;
    }

    public int ObstacleCount { get; }
    public int NodeCount { get; }
    public int EdgeCount { get; }
    public long VisibilityTests { get; }
    public long VisibleConnections { get; }
    public long ObstacleCandidateChecks { get; }
    public long TerrainCostSamples { get; }
    public TimeSpan Elapsed { get; }
}
