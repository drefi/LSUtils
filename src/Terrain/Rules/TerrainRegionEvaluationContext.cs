namespace LSUtils.Terrain.Rules;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Terrain;

public sealed class TerrainRegionEvaluationContext<TTerrainType, TContentType> {
    private readonly TerrainRegion<TTerrainType, TContentType> _region;

    public TerrainRegion<TTerrainType, TContentType> Region => _region;
    public IReadOnlyCollection<TerrainPatch<TTerrainType>> Patches => _region.Patches;
    public IReadOnlyCollection<TerrainContent<TContentType>> Contents => _region.Contents;
    public float Area => _region.Area;

    public TerrainRegionEvaluationContext(TerrainRegion<TTerrainType, TContentType> region) {
        _region = region ?? throw new LSArgumentNullException(nameof(region));
    }

    public float GetPatchArea(TTerrainType type) {
        var comparer = EqualityComparer<TTerrainType>.Default;
        return _region.Patches
            .Where(patch => comparer.Equals(patch.Type, type))
            .Sum(patch => patch.Area);
    }

    public float GetPatchAreaRatio(TTerrainType type) {
        return Area <= 0f ? 0f : GetPatchArea(type) / Area;
    }

    public int GetPatchCount(TTerrainType type) {
        var comparer = EqualityComparer<TTerrainType>.Default;
        return _region.Patches.Count(patch => comparer.Equals(patch.Type, type));
    }

    public float GetContentArea(TContentType type) {
        var comparer = EqualityComparer<TContentType>.Default;
        return _region.Contents
            .Where(content => comparer.Equals(content.Type, type))
            .Sum(content => content.Area);
    }

    public float GetContentAreaRatio(TContentType type) {
        return Area <= 0f ? 0f : GetContentArea(type) / Area;
    }

    public int GetContentCount(TContentType type) {
        var comparer = EqualityComparer<TContentType>.Default;
        return _region.Contents.Count(content => comparer.Equals(content.Type, type));
    }
}
