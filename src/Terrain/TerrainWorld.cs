namespace LSUtils.Terrain;

using System.Collections.Generic;
using System.Linq;
using LSUtils;
using LSUtils.Terrain.Navigation;
using LSUtils.Spatial;

public class TerrainWorld<TTerrainType, TContentType> {
    private readonly ISpatialIndex<TerrainPatch<TTerrainType>> _patchIndex;
    private readonly ISpatialIndex<TerrainContent<TContentType>> _contentIndex;
    private readonly HashSet<TerrainPatch<TTerrainType>> _patches = new();
    private readonly HashSet<TerrainContent<TContentType>> _contents = new();
    private readonly HashSet<TerrainRegion<TTerrainType, TContentType>> _regions = new();

    public Bounds Bounds { get; }
    public TTerrainType DefaultTerrainType { get; }
    public IReadOnlyCollection<TerrainPatch<TTerrainType>> Patches => _patches;
    public IReadOnlyCollection<TerrainContent<TContentType>> Contents => _contents;
    public IReadOnlyCollection<TerrainRegion<TTerrainType, TContentType>> Regions => _regions;

    public TerrainWorld(Bounds bounds, TTerrainType defaultTerrainType, float spatialCellSize = 64f) {
        Bounds = bounds;
        DefaultTerrainType = defaultTerrainType;
        _patchIndex = new SpatialHashGrid<TerrainPatch<TTerrainType>>(spatialCellSize);
        _contentIndex = new SpatialHashGrid<TerrainContent<TContentType>>(spatialCellSize);
    }

    public bool AddPatch(TerrainPatch<TTerrainType> patch) {
        if (!_patches.Add(patch)) return false;
        _patchIndex.Insert(patch);
        return true;
    }

    public bool RemovePatch(TerrainPatch<TTerrainType> patch) {
        if (!_patches.Remove(patch)) return false;
        _patchIndex.Remove(patch);
        return true;
    }

    public bool UpdatePatch(TerrainPatch<TTerrainType> patch) {
        return _patches.Contains(patch) && _patchIndex.Update(patch);
    }

    public bool AddContent(TerrainContent<TContentType> content) {
        if (!_contents.Add(content)) return false;
        _contentIndex.Insert(content);
        return true;
    }

    public bool RemoveContent(TerrainContent<TContentType> content) {
        if (!_contents.Remove(content)) return false;
        _contentIndex.Remove(content);
        return true;
    }

    public bool UpdateContent(TerrainContent<TContentType> content) {
        return _contents.Contains(content) && _contentIndex.Update(content);
    }

    public bool AddRegion(TerrainRegion<TTerrainType, TContentType> region) {
        return _regions.Add(region);
    }

    public bool RemoveRegion(TerrainRegion<TTerrainType, TContentType> region) {
        return _regions.Remove(region);
    }

    public List<TerrainPatch<TTerrainType>> QueryPatches(Bounds area) {
        return _patchIndex.Query(area);
    }

    public List<TerrainContent<TContentType>> QueryContents(Bounds area) {
        return _contentIndex.Query(area);
    }

    public List<TerrainPatch<TTerrainType>> QueryPatchesAt(float x, float y) {
        return QueryPatches(new Bounds(x, y, 0, 0))
            .Where(patch => patch.Contains(x, y))
            .ToList();
    }

    public TTerrainType ResolveTerrainTypeAt(float x, float y) {
        var patch = ResolvePatchAt(x, y);
        return patch == null ? DefaultTerrainType : patch.Type;
    }

    public TerrainPatch<TTerrainType>? ResolvePatchAt(float x, float y) {
        return QueryPatchesAt(x, y)
            .OrderByDescending(patch => patch.Layer)
            .ThenByDescending(patch => patch.Priority)
            .FirstOrDefault();
    }

    public List<LSVector2> FindPath(
        LSVector2 start,
        LSVector2 goal,
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        return TerrainPathfinder.FindPath(this, start, goal, settings);
    }
}
