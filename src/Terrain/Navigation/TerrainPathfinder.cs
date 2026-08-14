namespace LSUtils.Terrain.Navigation;

using System.Collections.Generic;

/// <summary>Creates continuous polygon navigation meshes for terrain worlds.</summary>
public static class TerrainPathfinder {
    public static TerrainNavigationMesh<TTerrainType, TContentType> BakeNavigationMesh<TTerrainType, TContentType>(
        TerrainWorld<TTerrainType, TContentType> world,
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        if (world == null) throw new LSArgumentNullException(nameof(world));
        if (settings == null) throw new LSArgumentNullException(nameof(settings));
        return new TerrainNavigationMesh<TTerrainType, TContentType>(world, settings);
    }

    public static TerrainNavigationMesh<TTerrainType, TContentType> BuildNavigationMesh<TTerrainType, TContentType>(
        TerrainWorld<TTerrainType, TContentType> world,
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        return BakeNavigationMesh(world, settings);
    }

    /// <summary>Compatibility helper that builds a mesh for one path query.</summary>
    public static List<LSVector2> FindPath<TTerrainType, TContentType>(
        TerrainWorld<TTerrainType, TContentType> world,
        LSVector2 start,
        LSVector2 goal,
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        return BakeNavigationMesh(world, settings).FindPath(start, goal);
    }
}
